using System;
using System.Collections;
using Microsoft.Samples.Debugging.MdbgEngine;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using StackDump.Entities;
using StackDump.Extensions;

namespace StackDump
{
    public class StackDump
    {
        private static void Main(string[] args)
        {
            var allApplications = GetIisApplications();
            var workerProcesses = GetIisWorkerProcesses();

            foreach(var workerProcess in workerProcesses)
            {
                var applications = allApplications.Where(a => a.AppPool == workerProcess.AppPool);

                if (applications.Count() > 1)
                {
                    WriteYellow($"{workerProcess.AppPool} (sites: {string.Join(", ", applications.Select(a => a.Name))})");
                }
                else
                {
                    WriteYellow(applications.Single().Name);
                }

                var debugger = new MDbgEngine();

                using (var proc = new DebugProcess(debugger.Attach(workerProcess.Id, MdbgVersionPolicy.GetDefaultAttachVersion(workerProcess.Id))))
                {
                    InitializeMdbg(debugger, proc.Process);

                    var activeThreads = proc.Process.Threads.Cast<MDbgThread>().Where(t => t.IsActive());

                    if (!activeThreads.Any())
                    {
                        Console.WriteLine("  (no active .NET threads)");

                        continue;
                    }

                    foreach (var thread in activeThreads)
                    {
                        DumpThread(thread);
                    }
                }
            }
        }

        private static void WriteYellow(string value)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(value);
            Console.ForegroundColor = color;
        }

        private static void WriteBlue(string value)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(value);
            Console.ForegroundColor = color;
        }

        private static IEnumerable<IisWorkerProcess> GetIisWorkerProcesses()
        {
            return IisWorkerProcess.CreateFrom(GetAppCmdList("list wps"));
        }

        private static IEnumerable<IisApplication> GetIisApplications()
        {
            return IisApplication.CreateFrom(GetAppCmdList("list app"));
        }

        private static IEnumerable<string> GetAppCmdList(string command)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $@"{Environment.GetEnvironmentVariable("systemroot")}\system32\inetsrv\appcmd.exe",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                yield return proc.StandardOutput.ReadLine();
            }
        }

        private static void InitializeMdbg(MDbgEngine debugger, MDbgProcess proc)
        {
            var stopOnNewThread = debugger.Options.StopOnNewThread;
            debugger.Options.StopOnNewThread = false;
            proc.Go().WaitOne();
            debugger.Options.StopOnNewThread = true;
            while (proc.CorProcess.HasQueuedCallbacks(null))
            {
                proc.Go().WaitOne();
            }
            debugger.Options.StopOnNewThread = stopOnNewThread;
        }
        
        private static void DumpThread(MDbgThread thread)
        {
            Console.WriteLine();
            WriteBlue("Thread:");

            foreach (var frame in thread.Frames.Where(f => !f.IsInfoOnly && f.Function != null))
            {
                if (frame.Function.FullName == "System.Threading.Tasks.Task.Execute")
                {
                    return;
                }

                if (frame.Function.FullName == "System.Web.UI.Page.ProcessRequest")
                {
                    return;
                }

                string output;

                output = $"  {frame.Function.FullName}({string.Join(", ", frame.Function.GetArguments(frame).Select(a => (a.TypeName != "N/A" ? a.TypeName + " " : string.Empty) + a.Name))})";

                if (output.Length > 80)
                {
                    output = $"  {frame.Function.FullName}({string.Join(", ", frame.Function.GetArguments(frame).Select(a => (a.TypeName != "N/A" ? a.TypeName.Split('.').Last() + " " : string.Empty) + a.Name))})";
                }

                if (output.Length > 80)
                {
                    output = $"  {frame.Function.FullName}({string.Join(", ", frame.Function.GetArguments(frame).Select(a => a.Name))})";
                }

                if (output.Length > 80)
                {
                    output = output.Substring(0, 80 - 5) + " ...";
                }

                var outputParts = output.Split(new char[] { '(' }, 2);

                var color = Console.ForegroundColor;

                var methodNameParts = outputParts[0].Split('.');

                if (methodNameParts.First() != "  System")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }

                Console.Write(methodNameParts.First());
                Console.ForegroundColor = color;
                Console.Write('.');

                if (methodNameParts.Count() > 2)
                {
                    Console.Write(string.Join(".", methodNameParts.Skip(1).Take(methodNameParts.Count() - 2)));
                    Console.Write('.');
                }


                if (methodNameParts.First() != "  System")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                Console.Write(methodNameParts.Last());
                Console.ForegroundColor = color;

                if (outputParts.Length > 1)
                {
                    Console.Write('(');
                    Console.Write(outputParts[1]);
                }

                Console.WriteLine();
            }
        }
    }
}
