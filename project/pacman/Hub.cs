﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using Shared;
using System.Net.Sockets;
using System.Net;

namespace pacman
{
    public class Hub: MarshalByRefObject, IClient
    {
        //Connection Information
        public Uri Address { get; set; }
        private const string resource = "Client";
        private TcpChannel channel;
        private Uri serverURL { get; set; }
        private IServer server { get; set; }

        //Current Session Information
        public Session CurrentSession { get; set; } // changed to property
        private ChatRoom currentChatRoom;

        // # refactor this variables..
        private int msecPerRound;
        private IGame game;


        //Interface complience
        public string Username { get { return CurrentSession.Username; } }
        public int Round { get { return CurrentSession.Round; } }
        public List<IClient> Peers { get { return currentChatRoom.Peers; }  }

        //Events

        public delegate void StartEventHandler(IStage e);
        public delegate void RoundActionsEventHandler(List<Shared.Action> actions, int score, int round);
        public delegate void GameEndEvent(IPlayer e);
        public delegate string GetStateHandler(int round);

        public event StartEventHandler OnStart;
        public event RoundActionsEventHandler OnRoundReceived;
        public event EventHandler OnDeath;
        public event GameEndEvent OnGameEnd;

        public GetStateHandler getStateHandler { get; set; } 

        public Hub(Uri serverURL, Uri address, int msecPerRound, IGame game)
        {
            if(serverURL == null)
            {
                throw new Exception("The serverURL must be provided.");
            }

            if(address == null)
            {
                address = new Uri("tcp://localhost:"+ FreeTcpPort().ToString());
            }

            this.game = game;
            this.msecPerRound = msecPerRound;
            this.serverURL = serverURL;
            Address = address;

            CurrentSession = new Session(game, msecPerRound);

            channel = new TcpChannel(address.Port);
            ChannelServices.RegisterChannel(channel, false);

            RemotingServices.Marshal(this, resource,
                typeof(Hub));

            server = (IServer)Activator.GetObject(
                typeof(IServer),
                this.serverURL.ToString() + "Server");
        }

        public Hub(Uri serverURL, int msecPerRound) : this(serverURL, null, msecPerRound, new SimpleGame()) {}


        static int FreeTcpPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }


        //Attempts to join the server using a username
        public JoinResult Join(string username)
        {
            if (CurrentSession.SessionStatus != Session.Status.PENDING)
            {
                throw new Exception("A session is already open");
            }
            CurrentSession.Username = username;
            JoinResult result = server.Join(username, Address);
            switch (result)
            {
                case JoinResult.QUEUED:
                    CurrentSession.SessionStatus = Session.Status.QUEUED;
                    currentChatRoom = new ChatRoom(CurrentSession);

                    this.CurrentSession.game.OnPlayHandler += () =>
                    {
                        this.SetPlay(this.CurrentSession.game.Move);
                    };

                    break;
            }
            
            return result;
        }

        public void SetPlay(Play play)
        {
            if(CurrentSession == null)
            {
                throw new Exception("The session hasn't started yet.");
            }
            server.SetPlay(Address, play, CurrentSession.Round);
        }

        public void Quit()
        {
            try
            {
                if (CurrentSession != null)
                {
                    server.Quit(Address);
                }
            }catch(Exception e)
            {
                // there is not connection
            }
        }


        //IClient Interface 

        void IClient.Start(IStage stage)
        {
            CurrentSession.SessionStatus = Session.Status.RUNNING;
            OnStart?.Invoke(stage);
        }

        void IClient.SendRound(List<Shared.Action> actions, int score, int round)
        {
            CurrentSession.Round = round;
            OnRoundReceived?.Invoke(actions, score, round);
            CurrentSession.game.Play(round);
        }

        void IClient.Died()
        {
            CurrentSession.SessionStatus = Session.Status.DIED;
            OnDeath?.Invoke(this, null);
        }

        void IClient.End(IPlayer winner)
        {
            CurrentSession.SessionStatus = Session.Status.ENDED;
            OnGameEnd?.Invoke(winner);
        }


        //IChatRoom

        public void SendMessage(string username, string message)
        {
            if (currentChatRoom == null)
            {
                throw new Exception("The session hasn't started.");
            }
            currentChatRoom.SendMessage(username, message);
            //todo tie events to the form
        }

        public void SetPeers(Dictionary<string, Uri> peers)
        {
            if (currentChatRoom == null)
            {
                throw new Exception("The session hasn't started.");
            }
            currentChatRoom.SetPeers(peers);
        }

        public void PublishMessage(string message)
        {
            if (currentChatRoom == null)
            {
                throw new Exception("The session hasn't started.");
            }
            currentChatRoom.PublishMessage(CurrentSession.Username, message);
        }

        public string GetState(int round)
        {
            return getStateHandler.Invoke(round);
        }
    }
}