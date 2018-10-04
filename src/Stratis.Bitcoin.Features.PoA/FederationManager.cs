﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.PoA
{
    public class FederationManager
    {
        /// <summary><c>true</c> in case current node is a federation member.</summary>
        public bool IsFederationMember { get; private set; }

        /// <summary>Key of current federation member. <c>null</c> if <see cref="IsFederationMember"/> is <c>false</c>.</summary>
        public Key FederationMemberKey { get; private set; }

        private readonly NodeSettings settings;

        private readonly PoANetwork network;

        private readonly PoAMiner miner;

        private readonly ILogger logger;

        public FederationManager(NodeSettings nodeSettings, Network network, PoAMiner miner, ILoggerFactory loggerFactory)
        {
            this.settings = nodeSettings;
            this.network = network as PoANetwork;
            this.miner = miner;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.LoadKey();

            if (this.FederationMemberKey != null)
            {
                // Loaded key has to be a key for current federation.
                if (!this.network.FederationPublicKeys.Contains(this.FederationMemberKey.PubKey))
                {
                    string message = "Key provided is not registered on the network!";

                    this.logger.LogCritical(message);
                    throw new Exception(message);
                }

                this.logger.LogInformation("Federation key pair was successfully loaded. Your public key is: {0}.", this.FederationMemberKey.PubKey);

                // Enable mining because we are a federation member.
                this.miner.StartMining();
            }
        }

        /// <summary>Loads federation key if it exists.</summary>
        private void LoadKey()
        {
            var keyTool = new KeyTool(this.settings.DataFolder);

            Key key = keyTool.LoadPrivateKey();

            this.IsFederationMember = key != null;
            this.FederationMemberKey = key;
        }
    }
}
