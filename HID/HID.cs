using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PicLoader
{
    public class HidDeviceException : Exception
    {
        public HidDeviceException() { }
        public HidDeviceException(string message) : base(message) { }
        public HidDeviceException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    

    public class HidDevice
    {
        public string DeviceId { get; private set; }
        public string DevicePath { get; private set; }
        //public bool FoundDevice { get { return DevicePath != null; } }

        private Guid InterfaceClassGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030"); 

        public HidDevice(string deviceId)
        {
            this.DeviceId = deviceId;
            this.DevicePath = null; // Unknown
        }

        /// <summary>
        /// Scans the computer for any USB devices matching th specified VID/PID DeviceID.
        /// Throws an exception if none are found
        /// </summary>
        public void Scan()
        {
            var deviceInterfaceDetailData = new SetupAPI.SP_DEVICE_INTERFACE_DETAIL_DATA();
            var deviceInterfaceData = new SetupAPI.SP_DEVICE_INTERFACE_DATA();
            var deviceInfoData = new SetupAPI.SP_DEVINFO_DATA();

            UInt32 interfaceIndex = 0;
            UInt32 dwRegType;
            UInt32 dwRegSize;
            UInt32 structureSize = 0;
            bool matchFound = false;

            //First populate a list of plugged in devices (by specifying "DIGCF_PRESENT"), which are of the specified class GUID. 
            IntPtr pDeviceInfoTable = SetupAPI.SetupDiGetClassDevs(
                ref InterfaceClassGuid,
                null,
                IntPtr.Zero,
                SetupAPI.DIGCF_PRESENT | SetupAPI.DIGCF_DEVICEINTERFACE
            );
            try
            {

                //Now look through the list we just populated.  We are trying to see if any of them match our device. 
                while (true)
                {
                    deviceInterfaceData.cbSize = (UInt32)Marshal.SizeOf(typeof(SetupAPI.SP_DEVICE_INTERFACE_DATA));

                    if (!SetupAPI.SetupDiEnumDeviceInterfaces(pDeviceInfoTable, IntPtr.Zero, ref InterfaceClassGuid, interfaceIndex, ref deviceInterfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == SetupAPI.ERROR_NO_MORE_ITEMS)
                            throw new HidDeviceException(String.Format("No HID devices found matching {0}", this.DeviceId));
                        else
                            throw new Win32Exception(error);
                    }

                    //Now retrieve the hardware ID from the registry.  The hardware ID contains the VID and PID, which we will then 
                    //check to see if it is the correct device or not.

                    //Initialize an appropriate SP_DEVINFO_DATA structure.  We need this structure for SetupDiGetDeviceRegistryProperty().
                    deviceInfoData.cbSize = (UInt32)Marshal.SizeOf(typeof(SetupAPI.SP_DEVINFO_DATA));
                    if (!SetupAPI.SetupDiEnumDeviceInfo(pDeviceInfoTable, interfaceIndex, ref deviceInfoData))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    //First query for the size of the hardware ID, so we can know how big a buffer to allocate for the data.
                    //SetupDiGetDeviceRegistryPropertyUM(DeviceInfoTable, &DevInfoData, SPDRP_HARDWAREID, &dwRegType, NULL, 0, &dwRegSize);
                    SetupAPI.SetupDiGetDeviceRegistryProperty(
                        pDeviceInfoTable,
                        ref deviceInfoData,
                        SetupAPI.SPDRP_HARDWAREID,
                        out dwRegType, null, 0, out dwRegSize);

                    //Allocate a buffer for the hardware ID.
                    byte[] propertyValueBuffer = new byte[dwRegSize];

                    //Retrieve the hardware IDs for the current device we are looking at.  PropertyValueBuffer gets filled with a 
                    //REG_MULTI_SZ (array of null terminated strings).  To find a device, we only care about the very first string in the
                    //buffer, which will be the "device ID".  The device ID is a string which contains the VID and PID, in the example 
                    //format "Vid_04d8&Pid_003f".
                    if (!SetupAPI.SetupDiGetDeviceRegistryProperty(pDeviceInfoTable, ref deviceInfoData, SetupAPI.SPDRP_HARDWAREID, out dwRegType, propertyValueBuffer, dwRegSize, out dwRegSize)) 
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

				    //Now check if the first string in the hardware ID matches the device ID of my USB device.
                    String deviceIdFromRegistry = System.Text.Encoding.Unicode.GetString(propertyValueBuffer);

				    //Convert both strings to lower case.  This makes the code more robust/portable across OS Versions
                    deviceIdFromRegistry = deviceIdFromRegistry.ToLowerInvariant();
                    var deviceIdToFind = this.DeviceId.ToLowerInvariant();
			
				    //Now check if the hardware ID we are looking at contains the correct VID/PID
                    matchFound = deviceIdFromRegistry.Contains(deviceIdToFind);
                    if (matchFound == true)
				    {
					    //Device must have been found.  (Goal: Open read and write handles)  In order to do this, we will need the actual device path first.
					    //We can get the path by calling SetupDiGetDeviceInterfaceDetail(), however, we have to call this function twice:  The first
					    //time to get the size of the required structure/buffer to hold the detailed interface data, then a second time to actually 
					    //get the structure (after we have allocated enough memory for the structure.)
                        deviceInterfaceDetailData = new SetupAPI.SP_DEVICE_INTERFACE_DETAIL_DATA();

                        if (IntPtr.Size == 8) // for 64 bit operating systems
                            deviceInterfaceDetailData.cbSize = 8;
                        else
                            deviceInterfaceDetailData.cbSize = (UInt32)(4 + Marshal.SystemDefaultCharSize); // for 32 bit systems

                        UInt32 bufferSize = 1000;
                        if (!SetupAPI.SetupDiGetDeviceInterfaceDetail(pDeviceInfoTable, ref deviceInterfaceData, ref deviceInterfaceDetailData, bufferSize, out structureSize, ref deviceInfoData))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        // Finally set the devicePath
                        this.DevicePath = deviceInterfaceDetailData.DevicePath;
                        return;
				    }

				    interfaceIndex++;
				    if(interfaceIndex == 10000000)	//Surely there aren't more than 10 million interfaces attached to a single PC.
				    {
					    //If execution gets to here, it is probably safe to assume some kind of unanticipated problem occurred.
					    //In this case, bug out, to avoid infinite blocking while(true) loop.
                        throw new Exception("Unknown Error in HidDevice.Open()");
				    }

				    //Keep looping until we either find a device with matching VID and PID, or until we run out of items, or some error is encountered.
                }

            }
            finally
            {
                //Clean up the old structure we no longer need.
                SetupAPI.SetupDiDestroyDeviceInfoList(pDeviceInfoTable);
            }
        }

        /// <summary>
        /// Creates a new WinApiFile object that allows reading from the HID device.
        /// Make sure you dispose of the file when finished.
        /// </summary>
        /// <returns>A WinApiFile object in read mode</returns>
        public WinApiFile GetReadFile()
        {
            if (this.DevicePath == null)
                throw new HidDeviceException("HID device not connected");

            return new WinApiFile(
                this.DevicePath,
                WinApiFile.DesiredAccess.GENERIC_READ,
                WinApiFile.ShareMode.FILE_SHARE_READ | WinApiFile.ShareMode.FILE_SHARE_WRITE,
                WinApiFile.CreationDisposition.OPEN_EXISTING);
        }

        /// <summary>
        /// Creates a new WinApiFile object that allows writing to the HID device.
        /// Make sure you dispose of the file when finished.
        /// </summary>
        /// <returns>A WinApiFile object in write mode</returns>
        public WinApiFile GetWriteFile()
        {
            if (this.DevicePath == null)
                throw new HidDeviceException("HID device not connected");

            return new WinApiFile(
                this.DevicePath,
                WinApiFile.DesiredAccess.GENERIC_WRITE,
                WinApiFile.ShareMode.FILE_SHARE_READ | WinApiFile.ShareMode.FILE_SHARE_WRITE,
                WinApiFile.CreationDisposition.OPEN_EXISTING);
        }
    }
}
