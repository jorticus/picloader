using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicLoader
{
    public class HexFileException : Exception
    {
        public HexFileException() { }
        public HexFileException(string message) : base(message) { }
        public HexFileException(string message, Exception innerException) : base(message, innerException) { }
    }


    /// <summary>
    /// Provides reading and writing functionality of Intel HEX format files.
    /// Data is loaded into 64KiB chunks of memory, set by the data address and ExtendedLinearAddress records.
    /// Total addressable memory is up to 4GiB using extended linear addressing.
    /// Note that the entire hex file is loaded into memory.
    /// </summary>
    public class HexFile
    {
        public string FileName { get; private set; }
        public UInt64 Size { get; private set; }

        public List<MemoryBlock> blocks;

        #region Constants

        private enum HexRecordType : byte { 
            Data = 0x00,
            EOF = 0x01,
            ExtendedSegmentAddress = 0x02,  // Not supported
            StartSegmentAddress = 0x03,     // Not supported
            ExtendedLinearAddress = 0x04,
            StartLinearAddress = 0x05,      // Not supported
        };

        //This is the number of bytes per line of the 
        const int HEX_FILE_BYTES_PER_LINE = 16;

        #endregion

        #region Structs

        /// <summary>
        /// Represents a parsed line of the hex file
        /// </summary>
        private struct HexLine
        {
            public byte recordLength;
            public UInt16 addressField;
            public HexRecordType recordType;
            public byte checksum;
            public String dataPayload;

            public override string ToString()
            {
                return String.Format("{0}: {1}", recordType.ToString(), dataPayload);
            }
        }

        /// <summary>
        /// Represents a 64KiB chunk of memory
        /// </summary>
        public class MemoryBlock
        {
            public UInt64 startAddress = 0;
            public UInt64 size = 0;
            public byte[] data = null;

            public MemoryBlock(UInt64 startAddress = 0)
            {
                // Each memory block can be up to 65KiB in size
                // This is because the maximum possible line address is 0xFFFF, or 65535.
                // Extended Linear Addressing can be used to increase this, but a new block
                // is created for each of those.
                data = new byte[65536];
                this.startAddress = startAddress;
            }

            public override string ToString()
            {
                return String.Format("<MemoryBlock at 0x{0:x}>", startAddress);
            }
        }

        #endregion

        #region Constructors

        public HexFile()
        {
            FileName = null;
            Size = 0;
            blocks = null;
        }

        /// <summary>
        /// Load the specified hex file into memory.
        /// Note that hex files can contain up to 4GiB of data,
        /// and this class loads all that data into RAM.
        /// </summary>
        /// <param name="fileName">File path to load</param>
        public HexFile(string fileName)
        {
            FileName = fileName;
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                LoadFromStream(fs);
            }
        }
        /// <summary>
        /// Load the hex file from a stream into memory.
        /// Note that hex files can contain up to 4GiB of data,
        /// and this class loads all that data into RAM.
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        public HexFile(Stream stream)
        {
            FileName = null;
            LoadFromStream(stream);
        }

        #endregion

        #region Support Functions

        // Validates the given hex line against the line checksum
        private bool ValidateChecksum(string line)
        {
            ulong checksumCalculated = 0;
            byte recordLength = Convert.ToByte(line.Substring(0, 2), 16);
            byte checksum = Convert.ToByte(line.Substring(((int)recordLength * 2) + 8, 2), 16);

            for (byte j = 0; j < (recordLength + 4); j++)
                checksumCalculated += Convert.ToByte(line.Substring(j * 2, 2), 16);
            checksumCalculated = (~checksumCalculated) + 1;

            return ((checksumCalculated & 0x000000FF) == checksum);
        }

        private byte CalculateChecksum(string line)
        {
            //TODO: for saving hex files
            return 0;
        }

        private HexLine ParseLine(string line)
        {
            line = line.Trim();

            if (line[0] != ':')
                throw new HexFileException("No leading ':' in row");

            //remove the ":" from the record
            line = line.Substring(1, (line.Length - 1));

            // Validate checksum
            if (!ValidateChecksum(line))
                throw new HexFileException("Invalid checksum");

            HexLine hexLine = new HexLine();

            hexLine.recordLength = Convert.ToByte(line.Substring(0, 2), 16);
            hexLine.addressField = Convert.ToUInt16(line.Substring(2, 4), 16);
            hexLine.recordType = (HexRecordType)Convert.ToByte(line.Substring(6, 2), 16);
            hexLine.dataPayload = line.Substring(8, hexLine.recordLength * 2);
            
            return hexLine;
        }

        #endregion

        #region Load/Save Functions

        private void LoadFromStream(Stream stream)
        {
            blocks = new List<MemoryBlock>();

            string line;
			bool hexFileEOF = false;

            var currentBlock = new MemoryBlock();
            UInt64 currentExtendedAddress = 0;
            Size = 0;

            using (var reader = new StreamReader(stream))
            {
                while (((line = reader.ReadLine()) != "") && (hexFileEOF == false))
                {
                    HexLine hexLine = ParseLine(line);
 
                    switch (hexLine.recordType)
                    {
                        case HexRecordType.EOF:
                            hexFileEOF = true;
                            break;

                        case HexRecordType.ExtendedLinearAddress:
                            currentExtendedAddress = Convert.ToUInt64(hexLine.dataPayload, 16) << 16;

                            // Add a new memory block section if
                            //   1: the current block has data (size > 0)
                            //   2: the next block has a different starting address to the current block
                            //   3: the next block address does not already exist in the array.
                            if ((currentBlock.size > 0) && (currentExtendedAddress != currentBlock.startAddress) && (blocks.Count(b => b.startAddress == currentExtendedAddress) == 0))
                            {
                                blocks.Add(currentBlock);
                                currentBlock = new MemoryBlock(currentExtendedAddress);
                            }
                            break;

                        case HexRecordType.Data:
                            // Calculate the maximum possible address using the given data
                            // Actual maximum is 65536 bytes (2^16).
                            UInt64 size = (ulong)hexLine.recordLength + (ulong)hexLine.addressField;
                            if (size > currentBlock.size)
                                currentBlock.size = size;

                            // Assuming each memory block is 65K in size,
                            // load the data buffer with data at the given address
                            uint offset = 0;
                            for (byte j = 0; j < hexLine.recordLength; j++ )
                            {
                                uint addr = (uint)hexLine.addressField + j - offset;

                                // Address exceeds the currently allocated data block,
                                // create a new block at the current address
                                if (addr >= currentBlock.data.Length) //65536
                                {
                                    // Limit the block size
                                    currentBlock.size = (uint)currentBlock.data.Length;

                                    // Split the memory block
                                    blocks.Add(currentBlock);
                                    currentBlock = new MemoryBlock(addr + currentExtendedAddress);

                                    // Wrap the address around into the new block
                                    offset = (uint)currentBlock.data.Length;
                                    addr -= offset;
                                }

                                currentBlock.data[addr] = Convert.ToByte(hexLine.dataPayload.Substring(j * 2, 2), 16);
                            }

                            // Note that if a data line is missing, the data bytes are simply left as '0'
                            //TODO: are they supposed to be set to 0xFF?

                            break;

                        default:
                            throw new HexFileException(String.Format("Unsupported hex record type '{0}'", hexLine.recordType.ToString()));
                    }
                }
            }

            // Finally add the last block used
            blocks.Add(currentBlock);

            Size = 0;
            foreach (var block in blocks)
                Size += block.size;


            /*Console.WriteLine("Num blocks: {0}", blocks.Count);

            UInt64 totalSize = 0;
            foreach (var block in blocks)
            {
                //Console.WriteLine("{2}\n\tstartAddress:{0}\n\tsize:{1}", block.startAddress, block.size, block.ToString());
                Console.WriteLine(block);
                totalSize += block.size;
            }
            Console.WriteLine("Total size:{0} bytes", totalSize);*/
            
        }
        private void SaveToStream(Stream stream)
        {
            throw new Exception("Unimplemented");
        }

        #endregion

        #region Memory Access

        /// <summary>
        /// Returns all memory for the specified memory region, as a single
        /// contiguous byte array.
        /// </summary>
        /// <param name="memoryAddress">Address of the memory region, in words</param>
        /// <param name="memorySize">Size of the memory region, in words</param>
        /// <param name="bytesPerAddress">Word size (ie. 2 for PIC24)</param>
        /// <returns>A buffer of length (memorySize*bytesPerAddress)</returns>
        public byte[] GetMemoryRegion(uint memoryAddress, uint memorySize, uint bytesPerAddress = 1)
        {
            // HEX data is arranged like this:
            // 0000: 00 02 04 00        
            // 0004: 00 00 00 00
            // 0008: 14 03 00 00
            // 000c: dc 02 00 00
            //
            // PicKit2 data is arranged like this:
            // 0000: 04 02 00           00: 00 04, 01: 02 00
            // 0002: 00 00 00
            // 0004: 00 03 14           04: 00 00, 05: 03 14
            // 0006: 00 02 dc
            //
            // bytesPerAddress = 2 for the PIC24 (16-bit)
            //

            if (blocks == null)
                throw new HexFileException("Hex file not loaded");

            var data = new byte[memorySize*bytesPerAddress];
            uint idx = 0;

            ulong startPicAddress = memoryAddress * bytesPerAddress;
            ulong endPicAddress = startPicAddress + (memorySize * bytesPerAddress);

            // Assumes blocks are ordered by address
            foreach (var block in this.blocks)
            {
                ulong addr = block.startAddress;
                for (uint i=0; i<block.size; i++)
                {
                    if (addr >= startPicAddress && addr < endPicAddress)
                    {
                        data[idx++] = block.data[i];
                    }

                    addr++;
                }
            }

            return data;
        }

        #endregion
    }
}
