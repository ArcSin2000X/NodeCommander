﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stratis.CoinmasterClient.Network;

namespace Stratis.CoinmasterClient.Config
{
    public class ClientNodeConfig
    {
        public NodeEndpointName NodeEndpoint { get; set; }
        public string DisplayName { get; set; }
        public string NodeConfig { get; set; }
        public string DataDir { get; set; }
        public string CodeDirectory { get; set; }
        public string ProjectDirectory { get; set; }
        public string StartupSwitches { get; set; }
        public string Agent { get; set; }
        public bool Enabled { get; set; }
        
        public string NetworkDirectory => Path.Combine(DataDir, NodeEndpoint.NodeNetworkName, NodeEndpoint.NodeBlockchainName);

        public string NodeConfigFullName => Path.Combine(DataDir, NodeEndpoint.NodeNetworkName, NodeEndpoint.NodeBlockchainName, NodeConfig);
        public string DaemonName => new DirectoryInfo(ProjectDirectory).Name;

        public ClientNodeConfig()
        {
        }

        public ClientNodeConfig(string nodeFullName) : this()
        {
            NodeEndpoint = new NodeEndpointName(nodeFullName);
            DisplayName = NodeEndpoint.NodeName;
        }

    }
}
