using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManyConsole;

namespace HIDBootloader.Commands
{
    class ScanCommand : ConsoleCommand
    {

        public ScanCommand()
        {
            IsCommand("scan", "Scans for connected devices");


        }

        public override int Run(string[] remainingArguments)
        {

            //throw new NotImplementedException();
            return 0;
        }

    }
}
