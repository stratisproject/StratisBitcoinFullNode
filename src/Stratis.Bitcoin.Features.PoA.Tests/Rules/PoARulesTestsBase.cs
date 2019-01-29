﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoARulesTestsBase
    {
        protected readonly ChainedHeader currentHeader;
        protected readonly PoANetwork network;
        protected readonly PoAConsensusOptions consensusOptions;

        protected PoAConsensusRuleEngine rulesEngine;
        protected readonly LoggerFactory loggerFactory;
        protected readonly PoABlockHeaderValidator poaHeaderValidator;
        protected readonly SlotsManager slotsManager;
        protected readonly ConsensusSettings consensusSettings;
        protected readonly ConcurrentChain chain;

        public PoARulesTestsBase(PoANetwork network = null)
        {
            this.loggerFactory = new LoggerFactory();
            this.network = network == null ? new PoANetwork() : network;
            this.consensusOptions = this.network.ConsensusOptions;

            this.chain = new ConcurrentChain(this.network);
            IDateTimeProvider timeProvider = new DateTimeProvider();
            this.consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network));

            this.slotsManager = new SlotsManager(this.network, new FederationManager(NodeSettings.Default(this.network), this.network, this.loggerFactory), this.loggerFactory);

            this.poaHeaderValidator = new PoABlockHeaderValidator(this.loggerFactory);

            this.rulesEngine = new PoAConsensusRuleEngine(this.network, this.loggerFactory, new DateTimeProvider(), this.chain,
                new NodeDeployments(this.network, this.chain), this.consensusSettings, new Checkpoints(this.network, this.consensusSettings), new Mock<ICoinView>().Object,
                new ChainState(), new InvalidBlockHashStore(timeProvider), new NodeStats(timeProvider), this.slotsManager, this.poaHeaderValidator);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(50, null, false, null, this.network);

            this.currentHeader = headers.Last();
        }

        public void InitRule(ConsensusRuleBase rule)
        {
            rule.Parent = this.rulesEngine;
            rule.Logger = this.loggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
        }
    }

    public class TestPoANetwork : PoANetwork
    {
        public TestPoANetwork(List<PubKey> pubKeysOverride = null)
        {
            var federationPublicKeys = new List<PubKey>()
            {
                new PubKey("02d485fc5ae101c2780ff5e1f0cb92dd907053266f7cf3388eb22c5a4bd266ca2e"),
                new PubKey("026ed3f57de73956219b85ef1e91b3b93719e2645f6e804da4b3d1556b44a477ef"),
                new PubKey("03895a5ba998896e688b7d46dd424809b0362d61914e1432e265d9539fe0c3cac0"),
                new PubKey("020fc3b6ac4128482268d96f3bd911d0d0bf8677b808eaacd39ecdcec3af66db34"),
                new PubKey("038d196fc2e60d6dfc533c6a905ba1f9092309762d8ebde4407d209e37a820e462"),
                new PubKey("0358711f76435a508d98a9dee2a7e160fed5b214d97e65ea442f8f1265d09e6b55")
            };

            if (pubKeysOverride != null)
                federationPublicKeys = pubKeysOverride;

            var baseOptions = this.Consensus.Options as PoAConsensusOptions;

            this.Consensus.Options = new PoAConsensusOptions(
                maxBlockBaseSize: baseOptions.MaxBlockBaseSize,
                maxStandardVersion: baseOptions.MaxStandardVersion,
                maxStandardTxWeight: baseOptions.MaxStandardTxWeight,
                maxBlockSigopsCost: baseOptions.MaxBlockSigopsCost,
                maxStandardTxSigopsCost: baseOptions.MaxStandardTxSigopsCost,
                federationPublicKeys: federationPublicKeys,
                targetSpacingSeconds: 60
            );
        }
    }
}
