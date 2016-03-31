using Microsoft.Samples.Debugging.MdbgEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StackDump.Extensions
{
    public static class MDbgThreadExtensions
    {
        public static bool IsIdle(this MDbgThread thread)
        {
            return thread.Frames?.First().Function?.FullName == "System.Threading.WaitHandle.WaitAny";
        }

        public static bool IsActive(this MDbgThread thread)
        {
            if (!thread.Frames.Any())
            {
                return false;
            }

            if (thread.IsIdle())
            {
                return false;
            }

            if (!thread.Frames.Any(f => !f.IsInfoOnly && f.Function != null))
            {
                return false;
            }

            return true;
        }
    }
}
