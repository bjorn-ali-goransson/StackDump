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
            var frames = thread.Frames.Where(f => !f.IsInfoOnly && f.Function != null).ToList();

            if (!frames.Any())
            {
                return false;
            }

            if (thread.IsIdle())
            {
                return false;
            }


            while (frames.First().Function.FullName.StartsWith("System.") || frames.First().Function.FullName.StartsWith("Microsoft.") || frames.First().Function.FullName.StartsWith("SNINativeMethodWrapper."))
            {
                frames.RemoveAt(0);
            }

            while (frames.Last().Function.FullName.StartsWith("System."))
            {
                frames.RemoveAt(frames.Count() - 1);
            }

            return frames.Any();
        }
    }
}
