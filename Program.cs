using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using ManyConsole;
using NDesk.Options;
using System.Text.RegularExpressions;

namespace PicLoader
{
    class Program
    {
        // Auto will scan HID and Serial devices, but not TCP or UDP (since there can be many addresses/ports)
        // In the future could scan TCP/UCP if they have some sort of broadcast beacon.
        public enum ProtocolType { Auto, HID, TCP, UDP, Serial };
        public enum ProgrammerAction { Scan, Erase, Read, Program, Verify, Reset, Run };

        //Modify this value to match the VID and PID in your USB device descriptor.
        //Use the formatting: "Vid_xxxx&Pid_xxxx" where xxxx is a 16-bit hexadecimal number.
        const string DEFAULT_HID_DEVICE_ID = "Vid_04d8&Pid_003c";

        struct Args
        {
            public ProgrammerAction action;
            public ProtocolType protocol;
            public string hexfile;
            public bool verbose;
            public bool showhelp;
            public bool programConfigs;
            public bool noVerify;
            public bool autoReset;
            public bool debug;

            public struct Hid {
                public string deviceId;
            }
            public Hid hid;

            public struct Network {
                public string address;
                public int port;
            }
            public Network network;

            public struct Serial {
                public string port;
                public int baud;
            }
            public Serial serial;
        }

        static void Main(string[] argv)
        {
            try
            {
                Args args = new Args();
                args.protocol = ProtocolType.HID;
                args.hexfile = null;


                #region Command Line Arguments

                var p_main = new OptionSet() {
                    //{ "v|verbose", v => args.verbose = (v != null) },
                    //{ "d|debug", "Show debug information", v => args.debug = (v != null)},
                    { "h|help", v => args.showhelp = (v != null) },

                    { "n|no-verify", "Don't verify on program", v => args.noVerify = (v != null)},
                    { "c|program-configs", "Program configuration bits", v => args.programConfigs = (v != null)},
                    { "r|reset", "Reset device on completion", v => args.autoReset = (v != null)},

                    // Protocol selection
                    /*{ "auto", "Automatically scan for devices (default)", v=> {if (v!=null) args.protocol = ProtocolType.Auto;}},
                    { "hid", "Use USB HID bootloader", v=> {if (v!=null) args.protocol = ProtocolType.HID;}},
                    { "tcp", "Use TCP network bootloader", v=> {if (v!=null) args.protocol = ProtocolType.TCP;}},
                    { "udp", "Use UDP network bootlaoder", v=> {if (v!=null) args.protocol = ProtocolType.UDP;}},
                    { "serial", "Use Serial bootloader", v=> {if (v!=null) args.protocol = ProtocolType.Serial;}},*/
                };

                var p_hid = new OptionSet()
                {
                    //{ "default", v=>{}},
                    { "device=", "Optional VID/PID of the USB Device\neg. VID_04D8&PID_003C (default value)", v => args.hid.deviceId = v},
                };
                /*var p_network = new OptionSet() {
                    { "addr=", "IP Address", v => args.network.address = v },
                    { "port=", "Port", (int v) => args.network.port = v },
                };
                var p_serial = new OptionSet() {
                    { "port=", "COM Port", v => args.serial.port = v},
                    { "baud=", "Baud Rate", (int v) => args.serial.baud = v},
                    // TODO: could add an option for multiple serial protocols?
                };*/

                #endregion

                List<string> subargs = p_main.Parse(argv);
                List<string> extra = subargs;

                #region Console Help

                if (args.showhelp) // --help
                {
                    ShowHelp(p_main, p_hid);
                    return;
                }

                #endregion

                #region Auto Protocol Scan

                // Try and search for devices automatically.
                // Order of search is: USB-HID, Serial, TCP, UDP
                // USB-HID uses the default DeviceID,
                // Serial uses common baud rates on all connected COM ports,
                // TCP/UDP looks for a broadcast from the device.
                if (args.protocol == ProtocolType.Auto)
                {
                    args.protocol = AutoProtocolScan();
                }

                #endregion

                #region Protocol Specific Argument Parsing

                // Parse protocol specific arguments for the specified protocol.
                // Does not apply for automatic scan, as parameters are determined automatically.
                // Also performs some basic verification of the parameters before doing anything with them.
                switch (args.protocol)
                {
                    case ProtocolType.HID:
                        extra = p_hid.Parse(subargs);

                        // Validate Device ID format
                        if (args.hid.deviceId != null)
                        {
                            if (!Regex.Match(args.hid.deviceId, @"vid_[0-9a-f]{4}&pid_[0-9a-f]{4}", RegexOptions.IgnoreCase).Success)
                                throw new OptionException(String.Format("Invalid USB-HID Device ID '{0}'", args.hid.deviceId), "device");
                        }
                        break;

                    case ProtocolType.TCP:
                    case ProtocolType.UDP:
                        throw new NotImplementedException("TCP/UDP Bootloader is not implemented yet");
                        //extra = p_network.Parse(subargs);

                        //TODO: validate IP address/port if specified
                        break;

                    case ProtocolType.Serial:
                        throw new NotImplementedException("Serial Bootloader is not implemented yet");
                        //extra = p_serial.Parse(subargs);

                        //TODO: validate COM port and baud rate if specified
                        //TODO: allow an auto com port?
                        break;
                }

                #endregion

                #region Un-named Argument Parsing

                // There are two un-named arguments, the first is the action to perform,
                // the second is a filename for the hex file to use (required for program/verify/read actions)

                // Must contain at least one extra parameter (action)
                if (extra.Count < 1)
                    throw new OptionException("Must specify an action", "action");
                args.action = (ProgrammerAction)Enum.Parse(typeof(ProgrammerAction), extra[0], true);

                // May contain a hexfile parameter (required for certain actions)
                if (args.action == ProgrammerAction.Program || args.action == ProgrammerAction.Read || args.action == ProgrammerAction.Verify)
                {
                    if (extra.Count < 2)
                        throw new OptionException("Must specify a hex file for this action", "hexfile");
                    args.hexfile = extra[1];
                }

                #endregion


                // Obtain a bootloader object for the given protocol, using polymorphism
                Bootloader bootloader = GetBootloaderObject(args);

                // Make sure the device is responding, and query it for device parameters
                bootloader.Query();
                Console.WriteLine("Found device");

                #region Action Execution

                // Run the specified action
                switch (args.action)
                {
                    case ProgrammerAction.Scan:
                        // Do nothing, as we need to scan for the device anyway.
                        //TODO: Show address/port/baud/etc. if auto-scanning
                        break;

                    case ProgrammerAction.Erase:
                        Console.WriteLine("Erasing program memory");
                        bootloader.Erase();
                        break;

                    case ProgrammerAction.Read:
                        {
                            Console.WriteLine("Reading program memory");
                            HexFile hexfile = bootloader.Read();
                            Console.WriteLine("Saved to '{0}'", args.hexfile);
                            //TODO: save hexfile
                        }
                        break;

                    case ProgrammerAction.Program:
                        {
                            HexFile hexfile = new HexFile(args.hexfile);
                            Console.WriteLine("Loaded HEX File '{0}' ({1} bytes)", args.hexfile, hexfile.Size);

                            if (hexfile.Size == 0)
                                throw new BootloaderException("Hex file is empty");

                            // Erase the device before programming (required)
                            Console.WriteLine("Erasing");
                            bootloader.Erase();

                            // Program the program memory, and optionally the config bits
                            Console.WriteLine("Programming");
                            bootloader.Program(hexfile, args.programConfigs);

                            // Optionally verify it was programmed correctly
                            if (!args.noVerify)
                            {
                                Console.WriteLine("Verifying");
                                bootloader.Verify(hexfile);
                            }
                        }
                        break;

                    case ProgrammerAction.Verify:
                        {
                            HexFile hexfile = new HexFile(args.hexfile);
                            Console.WriteLine("Verifying against '{0}'", args.hexfile);
                            bootloader.Verify(hexfile);
                        }
                        break;

                    case ProgrammerAction.Reset:
                        Console.WriteLine("Resetting device");
                        bootloader.Reset();
                        break;

                    case ProgrammerAction.Run:
                        Console.WriteLine("Running");
                        bootloader.Reset(); // TODO: implement
                        break;
                }

                // Reset after completing command
                if (args.autoReset && args.action != ProgrammerAction.Reset && args.action != ProgrammerAction.Run)
                {
                    Console.WriteLine("Resetting device");
                    bootloader.Reset();
                }

                #endregion

                //Console.WriteLine("Done.");

            }
#if !DEBUG
            /*catch (HidDeviceException e)
            {
                Console.Error.WriteLine("HID Error: {0}", e.Message);
            }*/
            catch (BootloaderException e)
            {
                Console.Error.WriteLine("Error: {0}", e.Message);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine("{0}", e.Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(e);
                Console.Error.WriteLine();
            }
#endif
            finally
            {
#if DEBUG
                // Pause the console so we can see the output
                Console.ReadKey();
#endif
            }
        }
        
        /// <summary>
        /// Obtain a bootloader object for the specified protocol
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>A subclassed object of Bootloader</returns>
        static Bootloader GetBootloaderObject(Args args)
        {
            // Obtain a bootloader object for the given protocol
            switch (args.protocol)
            {
                case ProtocolType.HID:
                    string deviceId = (args.hid.deviceId != null) ? args.hid.deviceId : DEFAULT_HID_DEVICE_ID;
                    return new HidBootloader(deviceId);

                case ProtocolType.TCP:
                case ProtocolType.UDP:
                    return null;

                case ProtocolType.Serial:
                    return null;

                default:
                    throw new Exception("Invalid protocol type");
            }
        }

        /// <summary>
        /// Automatically scan for devices using common settings.
        /// The scan performs the following actions:
        ///   1. Scan for HID devices matching common VID/PIDs
        ///   2. Scan for Serial devices on open COM ports at common baud rates (TODO: what baud rates)
        ///   3. Listen for special network broadcasts
        /// Throws an exception if no devices detected.
        /// </summary>
        /// <returns>The protocol detected, if any.</returns>
        static ProtocolType AutoProtocolScan()
        {
            throw new NotImplementedException("Auto protocol scan not implemented yet");
        }

        static void ShowHelp(OptionSet p_main, OptionSet p_hid)
        {
            Console.WriteLine("PIC HID Bootloader v0.1");
            Console.WriteLine("Author: Jared Sanson");
            Console.WriteLine();

            Console.WriteLine("Usage: picloader action [OPTIONS] [hexfile]");
            Console.WriteLine("Where action can be: scan, erase, read, program, verify, reset, run.");
            //Console.WriteLine("The protocol");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p_main.WriteOptionDescriptions(Console.Out);

            Console.WriteLine();
            Console.WriteLine("Options for --hid:");
            p_hid.WriteOptionDescriptions(Console.Out);

            /*Console.WriteLine();
            Console.WriteLine("Options for --tcp and --udp:");
            p_network.WriteOptionDescriptions(Console.Out);

            Console.WriteLine();
            Console.WriteLine("Options for --serial:");
            p_serial.WriteOptionDescriptions(Console.Out);*/

            Console.WriteLine();
            Console.WriteLine("Actions:");
            Console.WriteLine("        scan                 Scans for connected devices, but don't program.");
            Console.WriteLine("        erase                Erases the target device's memory.");
            Console.WriteLine("        read                 Reads the device's memory (if unproteted) and writes it to hexfile.");
            Console.WriteLine("        program              Programs hexfile to the target device.");
            Console.WriteLine("        verify               Verify the device's memory matches the specified hexfile.");
            Console.WriteLine("        reset                Resets the target device.");
            Console.WriteLine("        run                  For devices that support it, resets the device into the user firmware.");

            Console.ReadKey();
            return;
        }
    }
}
