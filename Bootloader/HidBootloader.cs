using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PicLoader
{


    class HidBootloader : Bootloader
    {
        public HidDevice HidDevice;

        public MemoryRegionStruct[] MemoryRegions { get { return memoryRegions; } }
        public DeviceFamilyType DeviceFamily { get { return deviceFamily; } }

        #region Constants


        

        public const int PIC24_RESET_REMAP_OFFSET = 0x1400;
        public const int MAX_DATA_REGIONS = 6;
        const int COMMAND_PACKET_SIZE = 65;

        const UInt32 ERROR_SUCCESS = 0;
        const UInt32 INVALID_HANDLE_VALUE = UInt32.MaxValue-1;

        #endregion

        #region Bootloader Command Constants
        //*********************** BOOTLOADER COMMANDS ******************************
        public const int QUERY_DEVICE = 0x02;
        public const int UNLOCK_CONFIG = 0x03;
        public const int ERASE_DEVICE = 0x04;
        public const int PROGRAM_DEVICE = 0x05;
        public const int PROGRAM_COMPLETE = 0x06;
        public const int GET_DATA = 0x07;
        public const int RESET_DEVICE = 0x08;
        public const int GET_ENCRYPTED_FF = 0xFF;
        public enum BootloaderCommand : byte { 
            QueryDevice = 0x02, 
            UnlockConfig = 0x03, 
            EraseDevice = 0x04, 
            ProgramDevice = 0x05, 
            ProgramComplete = 0x06, 
            GetData = 0x07, 
            ResetDevice = 0x08, 
            GetEncryptedFF = 0xFF
        };
        //**************************************************************************

        //*********************** QUERY RESULTS ************************************
        public const int QUERY_IDLE = 0xFF;
        public const int QUERY_RUNNING = 0x00;
        public const int QUERY_SUCCESS = 0x01;
        public const int QUERY_WRITE_FILE_FAILED = 0x02;
        public const int QUERY_READ_FILE_FAILED = 0x03;
        public const int QUERY_MALLOC_FAILED = 0x04;
        //**************************************************************************

        //*********************** PROGRAMMING RESULTS ******************************
        public const int PROGRAM_IDLE = 0xFF;
        public const int PROGRAM_RUNNING = 0x00;
        public const int PROGRAM_SUCCESS = 0x01;
        public const int PROGRAM_WRITE_FILE_FAILED = 0x02;
        public const int PROGRAM_READ_FILE_FAILED = 0x03;
        public const int PROGRAM_RUNNING_ERASE = 0x05;
        public const int PROGRAM_RUNNING_PROGRAM = 0x06;
        //**************************************************************************

        //*********************** ERASE RESULTS ************************************
        public const int ERASE_IDLE = 0xFF;
        public const int ERASE_RUNNING = 0x00;
        public const int ERASE_SUCCESS = 0x01;
        public const int ERASE_WRITE_FILE_FAILED = 0x02;
        public const int ERASE_READ_FILE_FAILED = 0x03;
        public const int ERASE_VERIFY_FAILURE = 0x04;
        public const int ERASE_POST_QUERY_FAILURE = 0x05;
        public const int ERASE_POST_QUERY_RUNNING = 0x06;
        public const int ERASE_POST_QUERY_SUCCESS = 0x07;
        //**************************************************************************

        //*********************** VERIFY RESULTS ***********************************
        public const int VERIFY_IDLE = 0xFF;
        public const int VERIFY_RUNNING = 0x00;
        public const int VERIFY_SUCCESS = 0x01;
        public const int VERIFY_WRITE_FILE_FAILED = 0x02;
        public const int VERIFY_READ_FILE_FAILED = 0x03;
        public const int VERIFY_MISMATCH_FAILURE = 0x04;
        //**************************************************************************

        //*********************** READ RESULTS *************************************
        public const int READ_IDLE = 0xFF;
        public const int READ_RUNNING = 0x00;
        public const int READ_SUCCESS = 0x01;
        public const int READ_READ_FILE_FAILED = 0x02;
        public const int READ_WRITE_FILE_FAILED = 0x03;
        //**************************************************************************

        //*********************** UNLOCK CONFIG RESULTS ****************************
        public const int UNLOCK_CONFIG_IDLE = 0xFF;
        public const int UNLOCK_CONFIG_RUNNING = 0x00;
        public const int UNLOCK_CONFIG_SUCCESS = 0x01;
        public const int UNLOCK_CONFIG_FAILURE = 0x02;
        //**************************************************************************

        //*********************** BOOTLOADER STATES ********************************
        public const int BOOTLOADER_IDLE = 0xFF;
        public const int BOOTLOADER_QUERY = 0x00;
        public const int BOOTLOADER_PROGRAM = 0x01;
        public const int BOOTLOADER_ERASE = 0x02;
        public const int BOOTLOADER_VERIFY = 0x03;
        public const int BOOTLOADER_READ = 0x04;
        public const int BOOTLOADER_UNLOCK_CONFIG = 0x05;
        public const int BOOTLOADER_RESET = 0x06;
        //**************************************************************************

        //*********************** RESET RESULTS ************************************
        public const int RESET_IDLE = 0xFF;
        public const int RESET_RUNNING = 0x00;
        public const int RESET_SUCCESS = 0x01;
        public const int RESET_WRITE_FILE_FAILED = 0x02;
        //**************************************************************************

        //*********************** MEMORY REGION TYPES ******************************
        public enum MemoryRegionType : byte { PROGRAM_MEM = 0x01, EEDATA = 0x02, CONFIG = 0x03, END = 0xFF };
        /*public const int MEMORY_REGION_PROGRAM_MEM = 0x01;
        public const int MEMORY_REGION_EEDATA = 0x02;
        public const int MEMORY_REGION_CONFIG = 0x03;
        public const int MEMORY_REGION_END = 0xFF;*/
        //**************************************************************************

        //*********************** HEX FILE CONSTANTS *******************************
        public const int HEX_FILE_EXTENDED_LINEAR_ADDRESS = 0x04;
        public const int HEX_FILE_EOF = 0x01;
        public const int HEX_FILE_DATA = 0x00;

        //This is the number of bytes per line of the 
        public const int HEX_FILE_BYTES_PER_LINE = 16;
        //**************************************************************************

        //*********************** Device Family Definitions ************************
        public enum DeviceFamilyType : byte { PIC18 = 1, PIC24 = 2, PIC32 = 3 };
        //**************************************************************************
        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MemoryRegionStruct
        {
            public MemoryRegionType Type;
            public UInt32 Address;
            public UInt32 Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct EnterBootloaderStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct QueryDeviceStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct QueryResultsStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public byte BytesPerPacket;
            public DeviceFamilyType DeviceFamily;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DATA_REGIONS)]
            public MemoryRegionStruct[] MemoryRegions;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct UnlockConfigStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public byte Setting;
        }

        /*[StructLayout(LayoutKind.Sequential, Size = COMMAND_PACKET_SIZE)]
        public struct EraseDeviceStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }*/

        const int PROGRAM_PACKET_DATA_SIZE = 58;
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct ProgramDeviceStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public UInt32 Address;
            public byte BytesPerPacket;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = PROGRAM_PACKET_DATA_SIZE)]
            public byte[] Data;
        }

        /*[StructLayout(LayoutKind.Sequential, Size = COMMAND_PACKET_SIZE)]
        public struct ProgramCompleteStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }*/

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct GetDataStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public UInt32 Address;
            public byte BytesPerPacket;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct GetDataResultsStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public UInt32 Address;
            public byte BytesPerPacket;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 58)]
            public byte[] Data;
        }

        /*[StructLayout(LayoutKind.Sequential, Size = COMMAND_PACKET_SIZE)]
        public struct ResetDeviceStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }*/

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct GetEncryptedFFResultsStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public byte blockSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 63)]
            public byte[] Data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct PacketDataStruct
        {
            public byte WindowsReserved;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] Data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct BootloaderCommandStruct
        {
            public byte WindowsReserved;
            public byte Command;
        }

        #endregion

        #region Private Variables

        byte encryptionBlockSize;
        byte[] encryptedFF;

        MemoryRegionStruct[] memoryRegions;

        byte bootloaderState;
        byte progressStatus;

        byte bytesPerInstructionWord;
        byte bytesPerAddressInHex;

        bool unlockStatus;

        byte bytesPerAddress = 0;
        byte bytesPerPacket = 0;

        DeviceFamilyType deviceFamily;

        #endregion

        #region Constructors

        public HidBootloader(string DeviceId)
        {
            HidDevice = new HidDevice(DeviceId);

            memoryRegions = new MemoryRegionStruct[MAX_DATA_REGIONS];

            unlockStatus = false;

            //Set the progress status bar to 0%
            progressStatus = 0;

            //Set the number of bytes per address to 0 until we perform
            //	a query and get the real results
            bytesPerAddress = 0;

        }

        public void Dispose()
        {

        }

        #endregion

        #region Device Commands

        /// <summary>
        /// Scan for a connected USB device, throws an exception if none connected
        /// </summary>
        private void Scan()
        {
            try
            {
                HidDevice.Scan();
            }
            catch (HidDeviceException)
            {
                throw new BootloaderException("Device not connected");
            }
        }

        /// <summary>
        /// This function queries the attached device for the programmable memory regions
        /// and stores the information returned into the memoryRegions array.
        /// </summary>
        public override void Query()
        {
            // Attempt to connect to the HidDevice
            this.Scan();

            if (HidDevice.DevicePath == null)
                throw new BootloaderException("HID device not connected");

            //Create the write file and read file handles the to the USB device
            //  that we want to talk to
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                using (var ReadDevice = HidDevice.GetReadFile())
                {
                    QueryDeviceStruct myCommand = new QueryDeviceStruct();
                    QueryResultsStruct myResponse = new QueryResultsStruct();

                    //Prepare the command that we want to send, in this case the QUERY
                    //  device command
                    myCommand.WindowsReserved = 0;
                    myCommand.Command = QUERY_DEVICE;

                    //Send the command that we prepared
                    WriteDevice.WriteStructure<QueryDeviceStruct>(myCommand);

                    //Try to read a packet from the device
                    myResponse = ReadDevice.ReadStructure<QueryResultsStruct>();

                    //If we were able to successfully read from the device

                    /*#if defined(DEBUG_THREADS) && defined(DEBUG_USB)
                        DEBUG_OUT("*** QUERY RESULTS ***");
                        printBuffer(myResponse.PacketData.Data,64);
                    #endif*/

                    //for each of the possible memory regions
                    var memRegions = new List<MemoryRegionStruct>();
                    for (byte i = 0; i < MAX_DATA_REGIONS; i++)
                    {
                        //If the type of region is 0xFF that means that we have
                        //  reached the end of the regions array.
                        if (myResponse.MemoryRegions[i].Type == MemoryRegionType.END)
                            break;

                        //copy the data from the packet to the local memory regions array
                        memRegions.Add(myResponse.MemoryRegions[i]);

                        /*#if defined(DEBUG_THREADS)
                            DEBUG_OUT(HexToString(memoryRegions[i].Type,1));
                            DEBUG_OUT(HexToString(memoryRegions[i].Address,4));
                            DEBUG_OUT(HexToString(memoryRegions[i].Size,4));
                            DEBUG_OUT("********************************************");
                        #endif*/

                    }
                    memoryRegions = memRegions.ToArray();

                    /*#if defined(DEBUG_THREADS)
                        DEBUG_OUT(HexToString(memoryRegionsDetected,1));
                    #endif*/

                    //copy the last of the data out of the results packet
                    switch ((DeviceFamilyType)myResponse.DeviceFamily)
                    {
                        case DeviceFamilyType.PIC18:
                            bytesPerAddress = 1;
                            //ckbox_ConfigWordProgramming_restore = true;
                            break;
                        case DeviceFamilyType.PIC24:
                            bytesPerAddress = 2;
                            //ckbox_ConfigWordProgramming_restore = true;
                            break;
                        case DeviceFamilyType.PIC32:
                            bytesPerAddress = 1;
                            //ckbox_ConfigWordProgramming_restore = false;
                            break;
                        default:
                            break;
                    }
                    deviceFamily = (DeviceFamilyType)myResponse.DeviceFamily;
                    bytesPerPacket = myResponse.BytesPerPacket;

                    /*#if defined(DEBUG_THREADS)
                        DEBUG_OUT("********************************************");
                        DEBUG_OUT(String::Concat("Bytes per address = 0x",HexToString(bytesPerAddress,1)));
                        DEBUG_OUT(String::Concat("Bytes per packet = 0x",HexToString(bytesPerPacket,1)));
                        DEBUG_OUT("********************************************");
                    #endif*/
                }
            }

        }

        #endregion

        #region Basic Commands
        /// <summary>
        /// Send a generic packet with no response
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        private void SendCommandPacket<T>(T packet)
        {
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                WriteDevice.WriteStructure<T>(packet);
            }
        }

        /// <summary>
        /// Resets the target device
        /// </summary>
        public override void Reset()
        {
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                WriteDevice.WriteStructure<BootloaderCommandStruct>(new BootloaderCommandStruct
                {
                    WindowsReserved = 0,
                    Command = RESET_DEVICE
                });
            }
        }

        /// <summary>
        /// Erases the target device
        /// </summary>
        public override void Erase()
        {
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                WriteDevice.WriteStructure<BootloaderCommandStruct>(new BootloaderCommandStruct
                {
                    WindowsReserved = 0,
                    Command = ERASE_DEVICE
                });
            }

            WaitForCommand();
        }

        /// <summary>
        /// Unlocks or Locks the target device's config bits for writing
        /// </summary>
        private void UnlockConfigBits(bool lockBits)
        {
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                WriteDevice.WriteStructure<UnlockConfigStruct>(new UnlockConfigStruct
                {
                    WindowsReserved = 0,
                    Command = UNLOCK_CONFIG,

                    //0x00 is sub-command to unlock the config bits
                    //0x01 is sub-command to lock the config bits
                    Setting = (lockBits) ? (byte)0x01 : (byte)0x00
                });
            }
        }

        /// <summary>
        /// Waits for the previous command to complete, by sending a test Query packet.
        /// </summary>
        private void WaitForCommand()
        {
            using (var WriteFile = HidDevice.GetWriteFile())
            {
                //If we were able to successfully send the erase command to
                //  the device then let's prepare a query command to determine
                //  when the is responding to commands again
                WriteFile.WriteStructure<QueryDeviceStruct>(new QueryDeviceStruct
                {
                    WindowsReserved = 0,
                    Command = QUERY_DEVICE
                });
            }

            using (var ReadFile = HidDevice.GetReadFile())
            {
                //Try to read a packet from the device
                ReadFile.ReadStructure<QueryResultsStruct>();
            }
        }

        #endregion

        #region Programming Commands

        public override HexFile Read()
        {
            return null;
        }

        public override void Verify(HexFile hex)
        {
            //throw new BootloaderException("Invalid byte at 0x{0:x}", 0);
        }

        /// <summary>
        /// Program the target device with the provided hexfile
        /// </summary>
        /// <param name="hexFile">Hexfile containing data to program</param>
        /// <param name="programConfigs">If true, will attempt to program config words (WARNING: programming invalid config words could brick the device!)</param>
        public override void Program(HexFile hexFile, bool programConfigs = false)
        {
            // Program config words first to minimise the risk that the MCU
            // is reset during programming, thus leaving the MCU in a state 
            // that can't be booted.
            if (programConfigs)
            {
                var configRegions = memoryRegions.Where(r => r.Type == MemoryRegionType.CONFIG);

                // Not all devices provide CONFIG memory regions, as it is usually not desirable to program them anyway.
                if (configRegions.Count() == 0)
                    throw new BootloaderException("Cannot program config words for this device (No CONFIG memory regions)");

                foreach (var memoryRegion in configRegions)
                {
                    ProgramMemoryRegion(hexFile, memoryRegion);
                }
            }

            // Program everything else (PROGMEM, EEDATA)
            var dataRegions = memoryRegions.Where(r => r.Type != MemoryRegionType.CONFIG);

            // This shouldn't happen in a properly configured device, but show in case it does to prevent confusion
            if (dataRegions.Count() == 0)
                throw new BootloaderException("Cannot program memory (No PROGMEM/EEDATA memory regions)");

            foreach (var memoryRegion in dataRegions)
            {
                ProgramMemoryRegion(hexFile, memoryRegion);
            }
        }

        /// <summary>
        /// Program the target PIC memory region using the provided hex file
        /// </summary>
        /// <param name="hexFile">Hexfile containing data to program</param>
        /// <param name="memoryRegion">The target memory region to program</param>
        private void ProgramMemoryRegion(HexFile hexFile, MemoryRegionStruct memoryRegion)
        {
            using (var WriteFile = HidDevice.GetWriteFile())
            {
                byte currentByteInAddress = 1;
                bool skippedBlock = false;

                // Obtain the data related to the current memory region
                var regionData = hexFile.GetMemoryRegion(memoryRegion.Address, memoryRegion.Size, bytesPerAddress);
                int j = 0;
                
                // While the current address is less than the end address
                uint address = memoryRegion.Address;
                uint endAddress = memoryRegion.Address + memoryRegion.Size;
                while (address < endAddress)
                {
                    // Prepare command
                    ProgramDeviceStruct myCommand = new ProgramDeviceStruct
                    {
                        WindowsReserved = 0,
                        Command = PROGRAM_DEVICE,
                        Address = address
                    };
                    myCommand.Data = new byte[PROGRAM_PACKET_DATA_SIZE];

                    // If a block consists of all 0xFF, then there is no need to write the block
                    // as the erase cycle will have set everything to 0xFF
                    bool skipBlock = true;

                    byte i;
                    for (i = 0; i < bytesPerPacket; i++)
                    {
                        byte data = regionData[j++];

                        myCommand.Data[i + (myCommand.Data.Length - bytesPerPacket)] = data;

                        if (data != 0xFF)
                        {
                            // We can skip a block if all bytes are 0xFF.
                            // Bytes are also ignored if it is byte 4 of a 3 word instruction on PIC24 (bytesPerAddress=2, currentByteInAddress=2, even address)

                            if ((bytesPerAddress != 2) || ((address%2)==0) || (currentByteInAddress!=2))
                            {
                                // Then we can't skip this block of data
                                skipBlock = false;
                            }
                        }

                        if (currentByteInAddress == bytesPerAddress)
                        {
                            // If we haven't written enough bytes per address to be at the next address
                            address++;
                            currentByteInAddress = 1;
                        }
                        else
                        {
                            // If we haven't written enough bytes to fill this address
                            currentByteInAddress++;
                        }

                        //If we have reached the end of the memory region, then we
                        //  need to pad the data at the end of the packet instead
                        //  of the front of the packet so we need to shift the data
                        //  to the back of the packet.
                        if (address >= endAddress)
                        {
                            byte n;
                            i++;

                            int len = myCommand.Data.Length;
                            for (n = 0; n < len; n++)
                            {
                                if (n < i)
                                    // Move it from where it is to the back of the packet, thus shifting all of the data down.
                                    myCommand.Data[len - n - 1] = myCommand.Data[i + (len - bytesPerPacket) - n - 1];
                                else
                                    myCommand.Data[len - n - 1] = 0x00;
                            }

                            // Break out of the for loop now that all the data has been padded out.
                            break;
                        }

                    }//end for

                    // Use the counter to determine how many bytes were written
                    myCommand.BytesPerPacket = i;

                    //If the block was all 0xFF then we can just skip actually programming
                    //  this device.  Otherwise enter the programming sequence
                    if (!skipBlock)
                    {
						//If we skipped one block before this block then we may need
						//  to send a proramming complete command to the device before
						//  sending the data for this command.
						if (skippedBlock)
                        {
                            SendCommandPacket<BootloaderCommandStruct>(new BootloaderCommandStruct
                            {
                                WindowsReserved = 0,
                                Command = PROGRAM_COMPLETE
                            });

                            //since we have now indicated that the programming is complete
                            //  then we now mark that we haven't skipped any blocks
                            skippedBlock = false;
                        }

                        // Write the packet data!
                        /*string debug = "";
                        foreach (byte b in myCommand.Data)
                            debug += b.ToString("x2") + " ";
                        Console.WriteLine(">>> USB OUT Packet >>>\n{0}", debug);*/

                        SendCommandPacket<ProgramDeviceStruct>(myCommand);
                    }
                    else
                    {
                        // We are skipping the block
                        skippedBlock = true;
                    }
                }//end while

                // All data for this region has been programmed
                SendCommandPacket<BootloaderCommandStruct>(new BootloaderCommandStruct
                {
                    WindowsReserved = 0,
                    Command = PROGRAM_COMPLETE
                });

            }//end using
        }

        #endregion
        
    

    }
}
