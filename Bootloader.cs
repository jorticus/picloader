using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HIDBootloader
{
    public class BootloaderException : Exception
    {
        public BootloaderException() { }
        public BootloaderException(string message) : base(message) { }
        public BootloaderException(string message, Exception innerException) : base(message, innerException) { }
    }

    class Bootloader : IDisposable
    {
        public HidDevice HidDevice;

        public MemoryRegionStruct[] MemoryRegions { get { return memoryRegions; } }
        public DeviceFamilyType DeviceFamily { get { return deviceFamily; } }

        #region Constants
        //Modify this value to match the VID and PID in your USB device descriptor.
        //Use the formatting: "Vid_xxxx&Pid_xxxx" where xxxx is a 16-bit hexadecimal number.
        public const string DEVICE_ID = "Vid_04d8&Pid_003c";

        

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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = COMMAND_PACKET_SIZE)]
        public struct ProgramDeviceStruct
        {
            public byte WindowsReserved;
            public byte Command;
            public UInt32 Address;
            public byte BytesPerPacket;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 58)]
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

        public Bootloader()
        {
            HidDevice = new HidDevice(DEVICE_ID);

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
        public void Scan()
        {
            try
            {
                HidDevice.Scan();
            }
            catch (HidDeviceException e)
            {
                throw new BootloaderException("Device not connected");
            }
        }

        /// <summary>
        /// This function queries the attached device for the programmable memory regions
        /// and stores the information returned into the memoryRegions array.
        /// </summary>
        public void Query()
        {
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
        public void Reset()
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
        public void Erase()
        {
            using (var WriteDevice = HidDevice.GetWriteFile())
            {
                WriteDevice.WriteStructure<BootloaderCommandStruct>(new BootloaderCommandStruct
                {
                    WindowsReserved = 0,
                    Command = ERASE_DEVICE
                });
            }
        }

        /// <summary>
        /// Unlocks or Locks the target device's config bits for writing
        /// </summary>
        public void UnlockConfigBits(bool lockBits)
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
        public void WaitForCommand()
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

        public HexFile Read()
        {
            return null;
        }

        public bool Verify(HexFile hex)
        {
            return true;
        }

        public void Program(HexFile hex, bool programConfigs=false)
        {
			UInt32 BytesWritten = 0;
			UInt32 BytesReceived = 0;

			//unsigned char* p;
			UInt32 address;
			UInt64 size;
			byte i,currentByteInAddress,currentMemoryRegion;
			bool configsProgrammed,everythingElseProgrammed;
			bool skipBlock,blockSkipped;

			configsProgrammed = false;
			everythingElseProgrammed = false;

            using (var WriteFile = HidDevice.GetWriteFile())
            {
                if (programConfigs == false)
                {
                    // we don't need to program the configuration bits
                    // so mark that we already have programmed them
                    configsProgrammed = true;
                }

                //While we haven't programmed everything in the device yet
                while (!configsProgrammed || !everythingElseProgrammed)
                {
                    for (currentMemoryRegion = 0; currentMemoryRegion < memoryRegions.Length; currentMemoryRegion++)
                    {
                        //If we haven't programmed the configuration words then we want
                        //  to do this first.  The problem is that if we have erased the
                        //  configuration words and we receive a device reset before we
                        //  reprogram the configuration words, then the device may not be
                        //  capable of running on the USB any more.  To try to minimize the
                        //  possibility of this occurrance, we first search all of the
                        //  memory regions and look for any configuration regions and program
                        //  these regions first.  This minimizes the time that the configuration
                        //  words are left unprogrammed.

                        //If the configuration words are not programmed yet
                        if (!configsProgrammed)
                        {
                            //If the current memory region is not a configuration section
                            //  then continue to the top of the for loop and look at the
                            //  next memory region.  We don't want to waste time yet looking
                            //  at the other memory regions.  We will come back later for
                            //  the other regions.
                            if (memoryRegions[currentMemoryRegion].Type != MemoryRegionType.CONFIG)
                                continue;
                        }
                        else
                        {
                            //If the configuration words are already programmed then if this
                            //  region is a configuration region then we want to continue
                            //  back to the top of the for loop and skip over this region.
                            //  We don't want to program the configuration regions twice.
                            if (memoryRegions[currentMemoryRegion].Type == MemoryRegionType.CONFIG)
                                continue;
                        }

                        //Get the address, size, and data for the current memory region
                        address = memoryRegions[currentMemoryRegion].Address;
                        size = memoryRegions[currentMemoryRegion].Size;
                        //p = getMemoryRegion(currentMemoryRegion);

                        //Mark that we intend to skip the first block unless we find a non-0xFF
                        //  byte in the packet
                        skipBlock = true;

                        //Mark that we didn't skip the last block
                        blockSkipped = false;

                        //indicate that we are at the first byte of the current address
                        currentByteInAddress = 1;

                        //while the current address is less than the end address
                        /*while (address < (memoryRegions[currentMemoryRegion].Address + memoryRegions[currentMemoryRegion].Size))
                        {
                            //prepare a program device command to send to the device
                            ProgramDevice myCommand = new ProgramDevice();
                            myCommand.WindowsReserved = 0;
                            myCommand.Command = PROGRAM_DEVICE;
                            myCommand.Address = address;

                            //Update the progress status with a percentage of how many
                            //  bytes are in the memory region vs how many have already been
                            //  programmed
                            //progressStatus = (unsigned char)(((100*(address - memoryRegions[currentMemoryRegion].Address)) / memoryRegions[currentMemoryRegion].Size));

                            //for as many bytes as we can fit in a packet
                            for(i=0; i<bytesPerPacket; i++)
                            {
                                byte data;

                                //load up the byte from the allocated memory into the packet
                                data = *p++;
                                myCommand.Data[i+(sizeof(myCommand.Data)-bytesPerPacket)] = data;

                                //if the byte wasn't 0xFF
                                if (data != 0xFF)
                                {
                                    if (bytesPerAddress == 2)
                                    {
                                        if ((address%2)!=0)
                                        {
                                            if (currentByteInAddress == 2)
                                            {
                                                //We can skip this block because we don't care about this byte
                                                //  it is byte 4 of a 3 word instruction on PIC24
                                                //myCommand.ProgramDevice.Data[i+(sizeof(myCommand.ProgramDevice.Data)-bytesPerPacket)] = 0;
                                            }
                                            else
                                            {
                                                //Then we can't skip this block of data
                                                skipBlock = false;
                                            }
                                        }
                                        else
                                        {
                                            //Then we can't skip this block of data
                                            skipBlock = false;
                                        }
                                    }
                                    else
                                    {
                                        //Then we can't skip this block of data
                                        skipBlock = false;
                                    }
                                }


                                if (currentByteInAddress == bytesPerAddress)
                                {
                                    //If we have written enough bytes per address to be
                                    //  at the next address, then increment the address
                                    //  variable and reset the count.  
                                    address++;
                                    currentByteInAddress = 1;
                                }
                                else
                                {
                                    //If we haven't written enough bytes to fill this 
                                    //  address then increment the number of bytes that
                                    //  we have added for this address
                                    currentByteInAddress++;
                                }

                                //If we have reached the end of the memory region, then we
                                //  need to pad the data at the end of the packet instead
                                //  of the front of the packet so we need to shift the data
                                //  to the back of the packet.
                                if (address >= (memoryRegions[currentMemoryRegion].Address + memoryRegions[currentMemoryRegion].Size))
                                {
                                    byte n;

                                    i++;

                                    //for each byte of the packet
                                    for (n=0; n<sizeof(myCommand.Data); n++)
                                    {
                                        if (n<i)
                                        {
                                            //move it from where it is to the the back of the packet thus
                                            //  shifting all of the data down
                                            myCommand.Data[sizeof(myCommand.Data)-n-1] = myCommand.ProgramDevice.Data[i+(sizeof(myCommand.ProgramDevice.Data)-bytesPerPacket)-n-1];
                                        }
                                        else
                                        {
                                            //set the remaining data values to 0
                                            myCommand.Data[sizeof(myCommand.Data)-n-1] = 0;

                                        }
                                    }

                                    //If this was the last address then break out of the for loop
                                    //  that is writing bytes to the packet
                                    break;
                                }
                            }

                            //The number of bytes programmed is still contained in the last loop
                            //  index, i.  Copy that number into the packet that is going to the device
                            myCommand.BytesPerPacket = i;

                            //If the block was all 0xFF then we can just skip actually programming
                            //  this device.  Otherwise enter the programming sequence
                            if(skipBlock == false)
                            {
                                //If we skipped one block before this block then we may need
                                //  to send a proramming complete command to the device before
                                //  sending the data for this command.
                                if(blockSkipped == true)
                                {
                                    WriteFile.WriteStructure<BootloaderCommand>(new BootloaderCommand {
                                        WindowsReserved = 0,
                                        Command = PROGRAM_COMPLETE
                                    });

                                    //since we have now indicated that the programming is complete
                                    //  then we now mark that we haven't skipped any blocks
                                    blockSkipped = false;
                                }

                                //#if defined(DEBUG_THREADS) && defined(DEBUG_USB)
                                //    DEBUG_OUT(">>> USB OUT Packet >>>");
                                //    printBuffer(myCommand.PacketData.Data,64);
                                //#endif

                                //Send the program command to the device
                                WriteFile.WriteStructure<ProgramDevice>(myCommand);

                                //initially mark that we are skipping the block.  We will
                                //  set this back to false on the first byte we find that is 
                                //  not 0xFF.
                                skipBlock = true;
                            }
                            else
                            {
                                //If we are skipping this block then mark that we have skipped
                                //  a block and initially mark that we will be skipping the
                                //  next block.  We will set skipBlock to false if we find
                                //  a byte that is non-0xFF in the next packet
                                blockSkipped = true;
                                skipBlock = true;
                            }
                        } //while

                        //Now that we are done with all of the addresses in this memory region,
                        //  before we move on we need to send a programming complete command to
                        //  the device.
                        WriteFile.WriteStructure<BootloaderCommand>(new BootloaderCommand
                        {
                            WindowsReserved = 0,
                            Command = PROGRAM_COMPLETE
                        });*/

                    }//for each memory region


                    if (configsProgrammed == false)
                    {
                        //If the configuration bits haven't been programmed yet then the first
                        //  pass through the for loop that just completed will have programmed
                        //  just the configuration bits so mark them as complete.
                        configsProgrammed = true;
                    }
                    else
                    {
                        //If the configuration bits were already programmed then this loop must
                        //  have programmed all of the other memory regions.  Mark everything
                        //  else as being complete.
                        everythingElseProgrammed = true;
                    }
                }//while
            }

            /*catch (Exception e)
            {
                if (e is Win32Exception || e is HidDeviceException)
                    throw new BootloaderException("Program Failed", e);
                else
                    throw;
            }*/
        }

        #endregion
        
    

    }
}
