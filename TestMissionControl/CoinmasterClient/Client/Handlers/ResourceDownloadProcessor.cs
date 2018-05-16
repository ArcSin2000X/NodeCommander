﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Stratis.CoinmasterClient.Messages;
using Stratis.CoinmasterClient.Network;

namespace Stratis.CoinmasterClient.Client.Handlers
{
    public class ResourceDownloadProcessor : RequestProcessorBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public List<Resource> ResourceList { get; set; }

        public ResourceDownloadProcessor(ClientConnection client) : base(client)
        {
        }

        public override void OpenEnvelope()
        {
            try
            {
                ResourceList = Message.GetPayload<List<Resource>>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Cannot deserialize Resource List message from agent {Client.Address}");
                return;
            }

            logger.Info($"Received Resource List from agent {Client.Address}");
        }

        public override void Process()
        {
            Client.Session.OnResourceDownloadUpdated(Client, ResourceList);
        }
    }
}
