﻿using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProcessCreationService
{
    class ProcessCreationService : MarshalByRefObject, IProcessCreationService
    {
        IDictionary<string, Process> processes = new Dictionary<string, Process>();
        IDictionary<string, bool> processesFrozen = new Dictionary<string, bool>();

        IDictionary<string, string> PIDToClientURL = new Dictionary<string, string>();
        IDictionary<string, string> PIDToServerURL = new Dictionary<string, string>();

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        public void Crash(string PID)
        {
            processes[PID].CloseMainWindow();
            PIDToClientURL.Remove(PID);
            PIDToServerURL.Remove(PID);
        }

        public void GlobalStatus()
        {
            Console.WriteLine("Process Status: ");
            foreach (string key in processes.Keys)
            {
                Process process = processes[key];
                if (process.HasExited)
                {
                    Console.WriteLine("PID {0} is down");
                }
                else if (processesFrozen.Keys.Contains(key))
                {
                    Console.WriteLine("PID {0} is frozen");
                }
                else
                {
                    Console.WriteLine("PID {0} is running");
                    // aqui agora vem a logica d
                    // as coisas estao implementadas no lado do Server e o Cliente?
                    //So no cliente acho eu 
                }
            }
        }

        //todo
        public void InjectDelay(string sourcePID, string destinationPID)
        {
            throw new NotImplementedException();
        }

        public string LocalState(string PID, string roundID)
        {
            IClient client = (IClient)Activator.GetObject(
                        typeof(IServer),
                        this.PIDToClientURL[PID]);
            return client.GetState(int.Parse(roundID));
        }

        public void StartClient(string PID, string clientURL, List<string> serverURLs)
        {
            Process clientProcess = new Process();
            processesFrozen.Add(PID, false);
            clientProcess.StartInfo.FileName = this.pathClientExecutable();
            string arguments = $"not-instructed {PID} {clientURL}";
            foreach (string url in serverURLs)
                arguments += " " + url;
            clientProcess.StartInfo.Arguments = arguments;
            processes[PID] = clientProcess;
            PIDToClientURL.Add(PID, clientURL);
            clientProcess.Start();
        }

        public void StartClient(string PID, string clientURL, string instructions, List<string> serverURLs)
        {
            Process clientProcess = new Process();
            processesFrozen.Add(PID, false);
            clientProcess.StartInfo.FileName = this.pathClientExecutable();
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(instructions);
            string base64Instructions = System.Convert.ToBase64String(plainTextBytes);
            string arguments = $"instructed {PID} {clientURL} {base64Instructions}";
            foreach (string url in serverURLs)
                arguments += " " + url;
            clientProcess.StartInfo.Arguments = arguments;
            processes.Add(PID, clientProcess);
            PIDToClientURL.Add(PID, clientURL);
            clientProcess.Start();
        }

        public void StartServer(string PID, string serverURL, string msecPerRound, string numPlayers, List<string> serverURLs)
        {
            Process serverProcess = new Process();
            processesFrozen[PID] = false;
            serverProcess.StartInfo.FileName = this.pathServerExecutable();
            string arguments = $"{PID} {serverURL} {msecPerRound} {numPlayers}";
            foreach (string url in serverURLs)
                arguments += " " + url;
            Console.WriteLine($"Starting server with args {arguments}");
            serverProcess.StartInfo.Arguments = arguments;
            processes[PID] = serverProcess;
            PIDToServerURL[PID]= serverURL;
            serverProcess.Start();
        }

        public void Freeze(string PID)
        {
            var process = processes[PID];
            Console.WriteLine("FREEZE");
            /* if (process.ProcessName == string.Empty)
                 return;
                 */
            processesFrozen[PID] = true;
            Console.WriteLine(process);
            foreach (ProcessThread pT in process.Threads)
            {
                Console.WriteLine("Freezing");
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                SuspendThread(pOpenThread);

                CloseHandle(pOpenThread);
            }
        }

        public void Unfreeze(string PID)
        {
            Process process = processes[PID];

            /*  if (process.ProcessName == string.Empty)
                  return;
                  */
            processesFrozen[PID] = false;

            foreach (ProcessThread pT in process.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                {
                    continue;
                }

                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);

                CloseHandle(pOpenThread);
            }
        }

        private string pathClientExecutable()
        {
            return isEnvDebug() ? @"..\..\..\pacman\bin\Debug\pacman.exe" : @"..\..\..\pacman\bin\Release\pacman.exe";
        }

        private string pathServerExecutable()
        {
            return isEnvDebug() ? @"..\..\..\Server\bin\Debug\Server.exe" : @"..\..\..\Server\bin\Release\Server.exe";
        }

        private bool isEnvDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
