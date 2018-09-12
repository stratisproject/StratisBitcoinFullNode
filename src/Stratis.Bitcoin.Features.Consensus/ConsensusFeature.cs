using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusFeature : FullNodeFeature
    {
        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly Signals.Signals signals;

        private readonly IConsensusManager consensusManager;

        private readonly NodeDeployments nodeDeployments;

        /// <summary>Consensus statistics logger.</summary>
        private readonly ConsensusStats consensusStats;

        public ConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            Signals.Signals signals,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments,
            ConsensusStats consensusStats,
            INodeStats nodeStats)
        {
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;
            this.consensusStats = consensusStats;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 1000);
        }

        private void AddInlineStats(StringBuilder benchLogs)
        {
            if (this.chainState?.ConsensusTip != null)
            {
                string log = "Consensus.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                    this.chainState.ConsensusTip.Height.ToString().PadRight(8) + " Consensus.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                    this.chainState.ConsensusTip.HashBlock;

                benchLogs.AppendLine(log);
            }
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            DeploymentFlags flags = this.nodeDeployments.GetFlags(this.consensusManager.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            this.signals.SubscribeForBlocksConnected(this.consensusStats);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
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
                    services.AddSingleton<ConsensusOptions, ConsensusOptions>();
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<ICoinView, CachedCoinView>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<ConsensusStats>();
                    services.AddSingleton<IConsensusRuleEngine, PowConsensusRuleEngine>();
                    services.AddSingleton<IChainState, ChainState>();
                    services.AddSingleton<ConsensusQuery>()
                        .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                        .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                    new PowConsensusRulesRegistration().RegisterRules(fullNodeBuilder.Network.Consensus);
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
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<StakeChainStore>().AddSingleton<IStakeChain, StakeChainStore>(provider => provider.GetService<StakeChainStore>());
                        services.AddSingleton<IStakeValidator, StakeValidator>();
                        services.AddSingleton<ConsensusController>();
                        services.AddSingleton<ConsensusStats>();
                        services.AddSingleton<IConsensusRuleEngine, PosConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                        new PosConsensusRulesRegistration().RegisterRules(fullNodeBuilder.Network.Consensus);
                    });
            });

            return fullNodeBuilder;
        }

        public class PowConsensusRulesRegistration : IRuleRegistration
        {
            public void RegisterRules(IConsensus consensus)
            {
                consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>()
                {
                    new HeaderTimeChecksRule(),
                    new CheckDifficultyPowRule(),
                    new BitcoinActivationRule(),
                    new BitcoinHeaderVersionRule()
                };

                consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>()
                {
                    new BlockMerkleRootRule()
                };

                consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>()
                {
                    new SetActivationDeploymentsPartialValidationRule(),

                    new TransactionLocktimeActivationRule(), // implements BIP113
                    new CoinbaseHeightActivationRule(), // implements BIP34
                    new WitnessCommitmentsRule(), // BIP141, BIP144
                    new BlockSizeRule(),

                    // rules that are inside the method CheckBlock
                    new EnsureCoinbaseRule(),
                    new CheckPowTransactionRule(),
                    new CheckSigOpsRule(),
                };

                consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
                {
                    new SetActivationDeploymentsFullValidationRule(),

                    // rules that require the store to be loaded (coinview)
                    new LoadCoinviewRule(),
                    new TransactionDuplicationActivationRule(), // implements BIP30
                    new PowCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                    new SaveCoinviewRule()
                };
            }
        }

        public class PosConsensusRulesRegistration : IRuleRegistration
        {
            public void RegisterRules(IConsensus consensus)
            {
                consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>()
                {
                    new HeaderTimeChecksRule(),
                    new HeaderTimeChecksPosRule(),
                    new StratisBigFixPosFutureDriftRule(),
                    new CheckDifficultyPosRule(),
                    new StratisHeaderVersionRule(),
                };

                consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>()
                {
                    new BlockMerkleRootRule(),
                    new PosBlockSignatureRule(),
                };

                consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>()
                {
                    new SetActivationDeploymentsPartialValidationRule(),

                    new CheckDifficultyHybridRule(),
                    new PosTimeMaskRule(),

                    // rules that are inside the method ContextualCheckBlock
                    new TransactionLocktimeActivationRule(), // implements BIP113
                    new CoinbaseHeightActivationRule(), // implements BIP34
                    new WitnessCommitmentsRule(), // BIP141, BIP144
                    new BlockSizeRule(),

                    new PosBlockContextRule(), // TODO: this rule needs to be implemented

                    // rules that are inside the method CheckBlock
                    new EnsureCoinbaseRule(),
                    new CheckPowTransactionRule(),
                    new CheckPosTransactionRule(),
                    new CheckSigOpsRule(),
                    new PosCoinstakeRule(),
                };

                consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
                {
                    new SetActivationDeploymentsFullValidationRule(),

                    // rules that require the store to be loaded (coinview)
                    new LoadCoinviewRule(),
                    new TransactionDuplicationActivationRule(), // implements BIP30
                    new PosCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                    new SaveCoinviewRule()
                };
            }
        }
    }
}