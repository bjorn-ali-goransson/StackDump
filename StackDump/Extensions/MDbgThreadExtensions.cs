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
        public static List<MDbgFrame> GetFrames(this MDbgThread thread)
        {
            var frames = thread.Frames.Where(f => !f.IsInfoOnly && f.Function != null).ToList();
            
            while (frames.Any() && (frames.First().Function.FullName.StartsWith("System.") || frames.First().Function.FullName.StartsWith("Microsoft.") || frames.First().Function.FullName.StartsWith("SNINativeMethodWrapper.")))
            {
                frames.RemoveAt(0);
            }

            while (frames.Any() && (frames.Last().Function.FullName.StartsWith("System.")))
            {
                frames.RemoveAt(frames.Count() - 1);
            }

            return frames;
        }
    }
}
