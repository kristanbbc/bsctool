using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bsc_tool_migrater
{
    class Program
    {
        static void Main(string[] args)
        {
//           Process.Start("iexplore.exe", @"http://software.er.bbc.co.uk/bsctool/setup.exe");
            Process.Start( @"\\bbcws3001\software\bsctool\setup.exe");
        }
    }
}
