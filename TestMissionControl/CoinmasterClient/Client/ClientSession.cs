﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using Stratis.CoinmasterClient.Client.Dispatchers;
using Stratis.CoinmasterClient.Client.Handlers;
using Stratis.CoinmasterClient.Client.Handlers.EventArgs;
using Stratis.CoinmasterClient.Messages;
using Stratis.CoinmasterClient.Network;

namespace Stratis.CoinmasterClient.Client
{
    public class ClientSession
    {
        public Dictionary<String, ClientConnection> Clients { get; set; }
        public NodeNetwork ManagedNodes { get; set; }

        public event Action<string, string> ConnectionStatusChanged;
        public event Action<ClientConnection, NodeNetwork> NodeStatsUpdated;
        public event Action<ClientConnection, AgentRegistration> AgentRegistrationUpdated;
        public event Action<ClientConnection, List<Resource>> ResourceDownloadUpdated;


        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        public void OnConnectionStatusChanged(string connectionAddress, string message = "")
        {
            if (ConnectionStatusChanged != null) ConnectionStatusChanged.Invoke(connectionAddress, message);
        }
        public void OnNodeStatsUpdated(ClientConnection clientConnection, NodeNetwork networkSegment)
        {
            if (NodeStatsUpdated != null) NodeStatsUpdated.Invoke(clientConnection, networkSegment);
        }

        public void OnAgentRegistrationUpdated(ClientConnection clientConnection, AgentRegistration registration)
        {
            if (AgentRegistrationUpdated != null) AgentRegistrationUpdated.Invoke(clientConnection, registration);
        }

        public void OnResourceDownloadUpdated(ClientConnection clientConnection, List<Resource> fileDownload)
        {
            if (ResourceDownloadUpdated != null) ResourceDownloadUpdated.Invoke(clientConnection, fileDownload);
        }

        public ClientSession()
        {
            ManagedNodes = new NodeNetwork();
            
            Clients = new Dictionary<String, ClientConnection>();
        }

        public void AddClient(ClientConnection client)
        {
            client.Session = this;
            client.OnOpen += () => ConnectionOpen(client);
            client.OnClose += () => ConnectionClose(client);
            client.OnConnectionError += (message) => ConnectionError(client, message);
            client.OnMessage += (payload) => MessageReceived(client, payload);

            if (!Clients.ContainsKey(client.Address))
                Clients.Add(client.Address, client);
        }

        public void ConnectClient(ClientConnection client)
        {
            client.Connect();
        }

        public void DisconnectClient(ClientConnection client)
        {
            client.Disconnect();
        }
        
        private void InitializeClientConnection(ClientConnection client)
        {
            //Configure processors
            client.Processors.Add(MessageType.AgentRegistration, new AgentRegistrationProcessor(client));
            client.Processors.Add(MessageType.NodeData, new NodeDataProcessor(client));
            client.Processors.Add(MessageType.FileDownload, new ResourceDownloadProcessor(client));

            //Configure dispatchers
            client.Dispatchers.Add(MessageType.ClientRegistration, new ClientRegistrationDispatcher(client, int.MaxValue));
            client.Dispatchers.Add(MessageType.NodeData, new NodeConfigurationDispatcher(client, int.MaxValue));
            client.Dispatchers.Add(MessageType.ActionRequest, new NodeActionDispatcher(client, 500));
            client.Dispatchers.Add(MessageType.DeployFile, new ResourceDeploymentDispatcher(client, 500));

            foreach (DispatcherBase dispatcher in client.Dispatchers.Values)
            {
                dispatcher.Updated += client.SendObject;
            }

            logger.Debug($"Connected Client {client.Address}");
        }

        private void ConnectionOpen(ClientConnection client)
        {
            InitializeClientConnection(client);
            foreach (DispatcherBase dispatcher in client.Dispatchers.Values) dispatcher.Start();

            logger.Info($"Connection opened for client {client.Address}");
            OnConnectionStatusChanged(client.Address);
        }


        private void ConnectionError(ClientConnection client, string message)
        {
            logger.Info($"Failed to connect to agent {client.Address}");
            OnConnectionStatusChanged(client.Address, message);
        }

        private void ConnectionClose(ClientConnection client)
        {
            logger.Info($"The connection is no longer available");
            OnConnectionStatusChanged(client.Address);

            client.Disconnect();
            if (Clients.ContainsKey(client.Address))
            {
                Clients.Remove(client.Address);
            }

            foreach (DispatcherBase dispatcher in client.Dispatchers.Values)
            {
                dispatcher.Updated -= client.SendObject;
            }

            foreach (DispatcherBase dispatcher in client.Dispatchers.Values) dispatcher.Stop();

            logger.Info($"Connection closed for agent {client.Address}");
        }

        private void MessageReceived(ClientConnection client, string payload)
        {
            MessageEnvelope envelope;
            try
            {
                envelope = JsonConvert.DeserializeObject<MessageEnvelope>(payload);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Cannot deserialize message envelope from agent {client.Address}");
                return;
            }

            try
            {
                logger.Trace($"Processing {envelope.MessageType} message");
                if (!client.Processors.ContainsKey(envelope.MessageType))
                {
                    logger.Fatal($"Unknown message type {envelope.MessageType} from agent {client.Address}");
                    return;
                }

                var processor = client.Processors[envelope.MessageType];
                processor.ProcessMessage(envelope);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Cannot process {envelope.MessageType} message from agent {client.Address}");
            }
        }
    }
}
