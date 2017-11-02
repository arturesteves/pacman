﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading.Tasks;
using Shared;

namespace pacman
{
    public class ClientManager
    {
        public string Username { get; set; }
        public int Port { get; set; }
        public string Server { get; set; }
        public bool Connected { get; private set; }
        public bool Joined { get; set; }
        public IServer server { get; private set; }
        public IClient client { get; private set; } // Client ServicesClientManager
        private TcpChannel channel;
        private ConcreteClient concretClient;
        private string resource;


        public ClientManager(string username)
        {
            this.Username = username;
            this.resource = "Client";
            //this.concretClient = new ConcreteClient();
        }


        //todo: catch exceptions
        public void createConnectionToServer()
        {
            if (!Connected)
            {
                //this.Port = this.server.NextAvailablePort(this.Address); // when this function is working on the server side is just to uncommment in this side.
                channel = new TcpChannel(this.Port);
                ChannelServices.RegisterChannel(channel, true);
                
                RemotingConfiguration.RegisterWellKnownServiceType(
                    typeof(ConcreteClient),
                    this.resource,
                WellKnownObjectMode.Singleton);                

                string address = String.Format("tcp://localhost:{0}/{1}", this.Port, this.resource);
                client = (IClient)Activator.GetObject(
                    typeof(IClient),
                    address);

                this.client.Address = address;

                server = (IServer)Activator.GetObject(
                    typeof(IServer),
                    "tcp://localhost:8086/Server");

                this.Connected = true;
                
            }
        }
    }
}
