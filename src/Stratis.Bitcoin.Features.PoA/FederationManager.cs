﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

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

        private readonly ILogger logger;

        public FederationManager(NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory)
        {
            this.settings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.network = Guard.NotNull(network as PoANetwork, nameof(network));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            // Load federation from the db.
            // TODO

            // Display federation.
            this.logger.LogInformation("Federation contains {0} members. Their public keys are: {1}",
                this.network.ConsensusOptions.GenesisFederationPublicKeys.Count, Environment.NewLine + string.Join(Environment.NewLine, this.network.ConsensusOptions.GenesisFederationPublicKeys));

            // Load key.
            Key key = new KeyTool(this.settings.DataFolder).LoadPrivateKey();

            this.IsFederationMember = key != null;
            this.FederationMemberKey = key;

            if (this.FederationMemberKey == null)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                return;
            }

            // Loaded key has to be a key for current federation.
            if (!this.network.ConsensusOptions.GenesisFederationPublicKeys.Contains(this.FederationMemberKey.PubKey))
            {
                string message = "Key provided is not registered on the network!";

                this.logger.LogCritical(message);
                throw new Exception(message);
            }

            this.logger.LogInformation("Federation key pair was successfully loaded. Your public key is: {0}.", this.FederationMemberKey.PubKey);
        }

        /// <summary>Provides up to date list of federation members.</summary>
        /// <remarks>
        /// Blocks that are not signed with private keys that correspond
        /// to public keys from this list are considered to be invalid.
        /// </remarks>
        public List<PubKey> GetFederationMembers()
        {
            // TODO replace with actual list
            return this.network.ConsensusOptions.GenesisFederationPublicKeys;
        }
    }
}
