using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManyConsole;

namespace HIDBootloader
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var commands = GetCommands();
                ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
                return;

                string filename = @"Zeitgeber.hex";

                Console.WriteLine("PIC HID Bootloader v0.1");
                Console.WriteLine("Author: Jared Sanson");
                Console.WriteLine();

                var hexfile = new HexFile(filename);
                Console.WriteLine("Loaded HEX File '{0}' ({1} bytes)", filename, hexfile.Size);

                if (hexfile.Size == 0)
                    throw new BootloaderException("Hex file is empty");

                using (var bootloader = new Bootloader())
                {
                    // Scan for the HID device
                    bootloader.Scan();
                    Console.WriteLine("Found HID device");

                    // Query device capabilities
                    bootloader.Query();
                    Console.WriteLine("Queried device ({1}). Found {0} memory regions", bootloader.MemoryRegions.Length, bootloader.DeviceFamily);
                    //Console.WriteLine();

                    Console.WriteLine("Erase");
                    bootloader.Erase();
                    bootloader.WaitForCommand();

                    Console.WriteLine("Program");
                    bootloader.Program(hexfile);

                    Console.WriteLine("Verify");
                    if (!bootloader.Verify(hexfile))
                        throw new BootloaderException("Verification Failed");

                    Console.WriteLine("Reset");
                    bootloader.Reset();
                }


                Console.WriteLine();
                Console.WriteLine("Done.");

            }
            catch (HidDeviceException e)
            {
                Console.Error.WriteLine("HID Error: {0}", e.Message);
            }
            catch (BootloaderException e)
            {
                Console.Error.WriteLine("Error: {0}", e.Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(e);
                Console.Error.WriteLine();
            }
            finally
            {
                Console.ReadKey();
            }
        }

        public static IEnumerable<ConsoleCommand> GetCommands()
        {
            return ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(typeof(Program));
        }
    }
}
