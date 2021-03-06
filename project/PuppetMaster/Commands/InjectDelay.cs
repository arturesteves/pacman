﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared;

namespace PuppetMaster.Commands
{
    public class InjectDelay : AsyncCommand
    {
        private delegate void injectDealyDel(string sourcePID, string destinationPID);
        private injectDealyDel remoteCallInjectDelay;
        public Dictionary<string, IProcessCreationService> processesPCS { get; set; }

        public InjectDelay() : base("InjectDelay") { }

        public override void CommandToExecute(string[] parameters)
        {
            string pid = parameters[0];
            IAsyncResult asyncResult;
            IProcessCreationService pcs = processesPCS[pid];

            remoteCallInjectDelay = new injectDealyDel(pcs.InjectDelay);
            asyncResult = remoteCallInjectDelay.BeginInvoke(pid, parameters[1], null, null);
            asyncResult.AsyncWaitHandle.WaitOne();
            remoteCallInjectDelay.EndInvoke(asyncResult);
        }
    }
}
