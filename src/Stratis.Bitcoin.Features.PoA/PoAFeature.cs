﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.PoA.Voting.ConsensusRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAFeature : FullNodeFeature
    {
        /// <summary>Manager of node's network connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        private readonly FederationManager federationManager;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly IConsensusManager consensusManager;

        /// <summary>A handler that can manage the lifetime of network peers.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        private readonly IPoAMiner miner;

        private readonly VotingManager votingManager;

        public PoAFeature(FederationManager federationManager, PayloadProvider payloadProvider, IConnectionManager connectionManager, ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory,
            IPoAMiner miner, VotingManager votingManager)
        {
            this.federationManager = federationManager;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.miner = miner;
            this.votingManager = votingManager;

            payloadProvider.DiscoverPayloads(this.GetType().Assembly);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;

            INetworkPeerBehavior defaultConsensusManagerBehavior = connectionParameters.TemplateBehaviors.FirstOrDefault(behavior => behavior is ConsensusManagerBehavior);
            if (defaultConsensusManagerBehavior == null)
            {
                throw new MissingServiceException(typeof(ConsensusManagerBehavior), "Missing expected ConsensusManagerBehavior.");
            }

            // Replace default ConsensusManagerBehavior with ProvenHeadersConsensusManagerBehavior
            connectionParameters.TemplateBehaviors.Remove(defaultConsensusManagerBehavior);
            connectionParameters.TemplateBehaviors.Add(new PoAConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory));

            this.federationManager.Initialize();

            if (this.federationManager.IsFederationMember)
            {
                // Enable mining because we are a federation member.
                this.miner.InitializeMining();
            }

            this.votingManager.Initialize();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.miner.Dispose();
        }
    }

    /// <summary>
    /// Factory for creating new consensus rules for POA.
    /// </summary>
    public class PoAConsensusRulesRegistration : IRuleRegistration
    {
        public RuleContainer CreateRules()
        {
            
            var headerValidationRules = new List<HeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksPoARule(),
                new StratisHeaderVersionRule(),
                new PoAHeaderDifficultyRule(),
                new PoAHeaderSignatureRule()
            };

            var integrityValidationRules = new List<IntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule(),
                new PoAIntegritySignatureRule()
            };

            var partialValidationRules = new List<PartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                // rules that are inside the method ContextualCheckBlock
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckSigOpsRule(),

                new PoAVotingCoinbaseOutputFormatRule(),
            };

            var fullValidationRules = new List<FullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                // rules that require the store to be loaded (coinview)
                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new PoACoinviewRule(),
                new SaveCoinviewRule()
            };

            return new RuleContainer(fullValidationRules, partialValidationRules, headerValidationRules, integrityValidationRules);
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderConsensusExtension
    {
        /// <summary>This is mandatory for all PoA networks.</summary>
        public static IFullNodeBuilder UsePoAConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PoAFeature>()
                    .DependOn<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<SlotsManager>();
                        services.AddSingleton<BlockDefinition, PoABlockDefinition>();
                    });
            });

            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<ConsensusController>();
                        services.AddSingleton<IConsensusRuleEngine, PoAConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

                        services.AddSingleton<IRuleRegistration, PoAConsensusRulesRegistration>();

                        // Voting.
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<VotingController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
