using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicLoader
{
    public class BootloaderException : Exception
    {
        public BootloaderException() { }
        public BootloaderException(string message) : base(message) { }
        public BootloaderException(string message, Exception innerException) : base(message, innerException) { }
    }

    abstract class Bootloader
    {
        public abstract void Query();
        public abstract void Reset();
        public abstract void Erase();
        public abstract HexFile Read();
        public abstract void Verify(HexFile hex);
        public abstract void Program(HexFile hex, bool programConfigs = false);

    }
}
