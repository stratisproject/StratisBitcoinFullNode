using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusFeature : FullNodeFeature, INodeStats
    {
        private readonly DBreezeCoinView dBreezeCoinView;

        private readonly LookaheadBlockPuller blockPuller;

        private readonly CoinView coinView;

        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly Signals.Signals signals;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        private readonly IConsensusLoop consensusLoop;

        private readonly NodeDeployments nodeDeployments;

        private readonly StakeChainStore stakeChain;

        private readonly IRuleRegistration ruleRegistration;

        private readonly NodeSettings nodeSettings;

        private readonly ConsensusSettings consensusSettings;

        private readonly IConsensusRules consensusRules;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Consensus statistics logger.</summary>
        private readonly ConsensusStats consensusStats;

        public ConsensusFeature(
            DBreezeCoinView dBreezeCoinView,
            Network network,
            LookaheadBlockPuller blockPuller,
            CoinView coinView,
            IChainState chainState,
            IConnectionManager connectionManager,
            Signals.Signals signals,
            IConsensusLoop consensusLoop,
            NodeDeployments nodeDeployments,
            ILoggerFactory loggerFactory,
            ConsensusStats consensusStats,
            IRuleRegistration ruleRegistration,
            IConsensusRules consensusRules,
            NodeSettings nodeSettings,
            ConsensusSettings consensusSettings,
            StakeChainStore stakeChain = null)
        {
            this.dBreezeCoinView = dBreezeCoinView;
            this.blockPuller = blockPuller;
            this.coinView = coinView;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.consensusLoop = consensusLoop;
            this.nodeDeployments = nodeDeployments;
            this.stakeChain = stakeChain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.consensusStats = consensusStats;
            this.ruleRegistration = ruleRegistration;
            this.nodeSettings = nodeSettings;
            this.consensusSettings = consensusSettings;
            this.consensusRules = consensusRules;

            this.chainState.MaxReorgLength = network.Consensus.Option<PowConsensusOptions>().MaxReorgLength;
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            if (this.chainState?.ConsensusTip != null)
            {
                benchLogs.AppendLine("Consensus.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                     this.chainState.ConsensusTip.Height.ToString().PadRight(8) +
                                     " Consensus.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                                     this.chainState.ConsensusTip.HashBlock);
            }
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.consensusSettings.Load(nodeSettings);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.dBreezeCoinView.InitializeAsync().GetAwaiter().GetResult();
            this.consensusLoop.StartAsync().GetAwaiter().GetResult();

            this.chainState.ConsensusTip = this.consensusLoop.Tip;
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            var flags = this.nodeDeployments.GetFlags(this.consensusLoop.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            this.stakeChain?.LoadAsync().GetAwaiter().GetResult();

            this.signals.SubscribeForBlocks(this.consensusStats);

            this.consensusRules.Register(this.ruleRegistration);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            ConsensusSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ConsensusSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // First, we need to wait for the consensus loop to finish.
            // Only then we can flush our coinview safely.
            // Otherwise there is a race condition and a new block
            // may come from the consensus at wrong time.
            this.consensusLoop.Stop();

            var cache = this.coinView as CachedCoinView;
            if (cache != null)
            {
                this.logger.LogInformation("Flushing Cache CoinView...");
                cache.FlushAsync().GetAwaiter().GetResult();
                cache.Dispose();
            }
           
            this.dBreezeCoinView.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderConsensusExtension
    {
        public static IFullNodeBuilder UsePowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            LoggingConfiguration.RegisterFeatureClass<ConsensusStats>("bench");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ConsensusFeature>()
                .FeatureServices(services =>
                {
                    // TODO: this should be set on the network build
                    fullNodeBuilder.Network.Consensus.Options = new PowConsensusOptions();

                    services.AddSingleton<ICheckpoints, Checkpoints>();
                    services.AddSingleton<NBitcoin.Consensus.ConsensusOptions, PowConsensusOptions>();
                    services.AddSingleton<IPowConsensusValidator, PowConsensusValidator>();
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<CoinView, CachedCoinView>();
                    services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>()); ;
                    services.AddSingleton<IConsensusLoop, ConsensusLoop>();
                    services.AddSingleton<ConsensusManager>().AddSingleton<INetworkDifficulty, ConsensusManager>();
                    services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();
                    services.AddSingleton<IGetUnspentTransaction, ConsensusManager>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<ConsensusStats>();
                    services.AddSingleton<ConsensusSettings>();
                    services.AddSingleton<IConsensusRules, PowConsensusRules>();
                    services.AddSingleton<IRuleRegistration, PowConsensusRulesRegistration>();
                });
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UsePosConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            LoggingConfiguration.RegisterFeatureClass<ConsensusStats>("bench");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        fullNodeBuilder.Network.Consensus.Options = new PosConsensusOptions();

                        if (fullNodeBuilder.Network.IsTest())
                        {
                            fullNodeBuilder.Network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity = 10;
                            fullNodeBuilder.Network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations = 10;
                        }

                        services.AddSingleton<ICheckpoints, Checkpoints>();
                        services.AddSingleton<IPowConsensusValidator, PosConsensusValidator>();
                        services.AddSingleton<IPosConsensusValidator, PosConsensusValidator>();
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<CoinView, CachedCoinView>();
                        services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>()); ;
                        services.AddSingleton<IConsensusLoop, ConsensusLoop>();
                        services.AddSingleton<StakeChainStore>().AddSingleton<IStakeChain, StakeChainStore>(provider => provider.GetService<StakeChainStore>());
                        services.AddSingleton<IStakeValidator, StakeValidator>();
                        services.AddSingleton<ConsensusManager>().AddSingleton<INetworkDifficulty, ConsensusManager>().AddSingleton<IGetUnspentTransaction, ConsensusManager>();
                        services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();
                        services.AddSingleton<ConsensusController>();
                        services.AddSingleton<ConsensusStats>();
                        services.AddSingleton<ConsensusSettings>();
                        services.AddSingleton<IConsensusRules, PosConsensusRules>();
                        services.AddSingleton<IRuleRegistration, PosConsensusRulesRegistration>();
                    });
            });

            return fullNodeBuilder;
        }

        public class PowConsensusRulesRegistration : IRuleRegistration
        {
            public IEnumerable<ConsensusRule> GetRules()
            {
                return new List<ConsensusRule>
                {
                    new BlockHeaderRule(),

                    // rules that are inside the method CheckBlockHeader
                    new CalculateWorkRule(),

                    // rules that are inside the method ContextualCheckBlockHeader
                    new CheckpointsRule(),
                    new AssumeValidRule(),
                    new BlockHeaderPowContextualRule(),

                    // rules that are inside the method ContextualCheckBlock
                    new TransactionLocktimeActivationRule(), // implements BIP113
                    new CoinbaseHeightActivationRule(), // implements BIP34
                    new WitnessCommitmentsRule(), // BIP141, BIP144
                    new BlockSizeRule(),

                    // rules that are inside the method CheckBlock
                    new BlockMerkleRootRule(),
                    new EnsureCoinbaseRule(),
                    new CheckPowTransactionRule(),
                    new CheckSigOpsRule(),

                    // rules that require the store to be loaded (coinview)
                    new LoadCoinviewRule(),
                    new TransactionDuplicationActivationRule() // implements BIP30
                };
            }
        }

        public class PosConsensusRulesRegistration : IRuleRegistration
        {
            public IEnumerable<ConsensusRule> GetRules()
            {
                return new List<ConsensusRule>
                {
                    new BlockHeaderRule(),

                    // rules that are inside the method CheckBlockHeader
                    new CalculateStakeRule(),

                    // rules that are inside the method ContextualCheckBlockHeader
                    new CheckpointsRule(),
                    new AssumeValidRule(),
                    new BlockHeaderPowContextualRule(),
                    new BlockHeaderPosContextualRule(),

                    // rules that are inside the method ContextualCheckBlock
                    new TransactionLocktimeActivationRule(), // implements BIP113
                    new CoinbaseHeightActivationRule(), // implements BIP34
                    new WitnessCommitmentsRule(), // BIP141, BIP144 
                    new BlockSizeRule(),

                    new PosBlockContextRule(), // TODO: this rule needs to be implemented

                    // rules that are inside the method CheckBlock
                    new BlockMerkleRootRule(),
                    new EnsureCoinbaseRule(),
                    new CheckPowTransactionRule(),
                    new CheckPosTransactionRule(),
                    new CheckSigOpsRule(),
                    new PosFutureDriftRule(),
                    new PosCoinstakeRule(),
                    new PosBlockSignatureRule(),

                    // rules that require the store to be loaded (coinview)
                    new LoadCoinviewRule(),
                    new TransactionDuplicationActivationRule() // implements BIP30
                };
            }
        }
    }
}
