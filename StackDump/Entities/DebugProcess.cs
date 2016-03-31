using Microsoft.Samples.Debugging.MdbgEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StackDump.Entities
{
    public class DebugProcess : IDisposable
    {
        public MDbgProcess Process { get; set; }

        public DebugProcess(MDbgProcess process)
        {
            Process = process;
        }

        public void Dispose()
        {
            if(Process == null)
            {
                return;
            }

            Process.Detach().WaitOne();
        }
    }
}
