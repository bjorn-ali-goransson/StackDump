using System;
using System.Collections;
using Microsoft.Samples.Debugging.MdbgEngine;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using StackDump.Entities;
using StackDump.Extensions;
using System.Security.Principal;
using System.Management;

namespace StackDump
{
    public static class StackDump
    {
        public static Regex siteNamePattern = new Regex("/site:\"(?<sitename>[^\"]+)\"", RegexOptions.Compiled);

        public static void Main(string[] args)
        {
            Console.WriteLine();

            var isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

            List<IisApplication> applications = null;
            List<IisWorkerProcess> workerProcesses = null;

            if (isAdmin)
            {
                workerProcesses = IisWorkerProcess.CreateFrom(GetAppCmdList("list wps"));
                applications = IisApplication.CreateFrom(GetAppCmdList("list app")).Where(a => workerProcesses.Any(w => w.AppPool == a.AppPool)).ToList();
            }
            else
            {
                var iisExpressSiteNames = new ManagementObjectSearcher("select * from Win32_Process where Name='iisexpress.exe'").Get().Cast<ManagementBaseObject>().ToDictionary(o => int.Parse(o["ProcessId"].ToString()), o => o["CommandLine"] == null ? null : siteNamePattern.Match(o["CommandLine"].ToString()).Groups["sitename"].Value);
                
                if(iisExpressSiteNames.ContainsValue(null))
                {
                    Console.WriteLine($"Warning: Some sites (PID {string.Join(", ", iisExpressSiteNames.Where(p => p.Value == null))}) seem to be running as Administrator and cannot be inspected");

                    iisExpressSiteNames = iisExpressSiteNames.Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value);
                }

                workerProcesses = Process.GetProcesses().Where(p => p.ProcessName == "iisexpress").Select(p => new IisWorkerProcess { AppPool = $"IIS Express ({p.Id})", Id = p.Id }).ToList();
                applications = workerProcesses.Select(w => new IisApplication { AppPool = w.AppPool, Name = iisExpressSiteNames[w.Id] }).ToList();
            }

            var processes = applications.GroupBy(a => workerProcesses.Single(p => p.AppPool == a.AppPool), (w, a) => new { Id = w.Id, AppPool = w.AppPool, Applications = a.Select(app => app.Name) });
            
            foreach(var process in processes)
            {
                if (process.Applications.Count() > 1)
                {
                    Console.WriteLine($"[{process.AppPool}] (sites: {string.Join(", ", process.Applications)})");
                }
                else
                {
                    Console.WriteLine($"[{process.Applications.Single()}]");
                }

                var debugger = new MDbgEngine();

                try
                {
                    var netVersion = MdbgVersionPolicy.GetDefaultAttachVersion(process.Id); // like "4.0.30319.42000" ... read more: https://msdn.microsoft.com/en-us/library/ms230176(v=vs.110).aspx
                    using (var proc = new DebugProcess(debugger.Attach(process.Id, netVersion)))
                    {
                        InitializeMdbg(debugger, proc.Process);

                        var activeThreads = proc.Process.Threads.Cast<MDbgThread>().Where(t => t.GetFrames().Any());

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
                catch (Exception e){
                    Console.WriteLine(e.Message);
                }
            }
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

            string lastNamespaceBase = null;
            string lastMethodName = null;

            foreach (var frame in thread.GetFrames())
            {
                if (frame.Function.FullName.Contains("+<>") || frame.Function.FullName.Contains(".<"))
                {
                    continue;
                }

                var color = Console.ForegroundColor;

                var methodNameParts = frame.Function.FullName.Split('.');

                var namespaceBase = methodNameParts.First().Trim();
                var namespaceRest = methodNameParts.Count() > 2 ? string.Join(".", methodNameParts.Skip(1).Take(methodNameParts.Count() - 2)) : null;
                var methodName = methodNameParts.Last();

                if (methodName.StartsWith("<"))
                {
                    continue;
                }

                if (namespaceBase != "System" && namespaceBase != lastNamespaceBase)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    lastMethodName = null;
                }

                Console.Write($"  {namespaceBase}");
                lastNamespaceBase = namespaceBase;

                Console.ForegroundColor = color;
                Console.Write('.');

                if (namespaceRest != null)
                {
                    Console.Write(namespaceRest);
                    Console.Write('.');
                }

                if (namespaceBase != "System" && methodName != lastMethodName)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                Console.Write(methodName);
                lastMethodName = methodName;
                Console.ForegroundColor = color;

                List<MDbgValue> arguments;

                try
                {
                    arguments = frame.Function.GetArguments(frame).ToList();
                }
                catch
                {
                    continue;
                }

                if(arguments.First().Name == "this")
                {
                    arguments.RemoveAt(0);
                }

                string argumentsString = string.Join(", ", arguments.Select(a => (a.TypeName == "N/A" ? string.Empty : a.TypeName.Split('.').Last() + " ") + a.Name));

                if(arguments.Any(a => a.TypeName == "N/A") || $"  {methodName}({argumentsString})".Length > 79)
                {
                    argumentsString = string.Join(", ", arguments.Select(a => a.Name));
                }

                if ($"  {methodName}({argumentsString})".Length > 79)
                {
                    while($"  {methodName}({argumentsString} ...".Length > 79)
                    argumentsString = argumentsString.Substring(0, argumentsString.Length - 1);
                }

                Console.Write('(');

                Console.Write(argumentsString);

                if (!argumentsString.EndsWith(" ..."))
                {
                    Console.Write(')');
                }

                Console.WriteLine();
            }
        }
    }
}
