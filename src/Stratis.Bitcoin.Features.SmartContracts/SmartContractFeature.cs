using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly IConsensusLoop consensusLoop;
        private readonly ContractStateRepositoryRoot stateRoot;

        public SmartContractFeature(IConsensusLoop consensusLoop, ILoggerFactory loggerFactory, ContractStateRepositoryRoot stateRoot)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stateRoot = stateRoot;
            this.consensusLoop = consensusLoop;
        }

        public override void Initialize()
        {
            this.stateRoot.SyncToRoot(((SmartContractBlockHeader)this.consensusLoop.Chain.Tip.Header).HashStateRoot.ToBytes());
            this.logger.LogInformation("Smart Contract Feature Injected.");
        }
    }

    public sealed class ReflectionVirtualMachineFeature : FullNodeFeature
    {
        private readonly IConsensusRules consensusRules;
        private readonly ILogger logger;

        public ReflectionVirtualMachineFeature(IConsensusRules consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("Reflection Virtual Machine Injected.");
            this.consensusRules.Register(new ReflectionRuleRegistration());
        }
    }

    public static partial class IFullNodeBuilderExtensions
    {
        public static ISmartContractVmBuilder AddSmartContracts(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SmartContractFeature>("smartcontracts");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SmartContractFeature>()
                    .DependOn<MiningFeature>()
                    .FeatureServices(services =>
                    {
                        // STATE ----------------------------------------------------------------------------
                        services.AddSingleton<DBreezeContractStateStore>();
                        services.AddSingleton<ISmartContractReceiptStorage, DBreezeContractReceiptStorage>();
                        services.AddSingleton<NoDeleteContractStateSource>();
                        services.AddSingleton<ContractStateRepositoryRoot>();

                        // BLOCK BUILDING--------------------------------------------------------------------
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, SmartContractBlockProvider>();
                        services.AddSingleton<SmartContractBlockDefinition>();

                        // CONSENSUS ------------------------------------------------------------------------
                        services.AddSingleton<IMempoolValidator, SmartContractMempoolValidator>();
                        services.AddSingleton<StandardTransactionPolicy, SmartContractTransactionPolicy>();

                        services.AddSingleton<InternalTransactionExecutorFactory>();
                        services.AddSingleton<ISmartContractVirtualMachine, ReflectionVirtualMachine>();

                        var callDataSerializer = CallDataSerializer.Default;
                        services.AddSingleton(callDataSerializer);
                        services.Replace(new ServiceDescriptor(typeof(IScriptAddressReader), 
                            new SmartContractScriptAddressReader(new ScriptAddressReader(), callDataSerializer)));
                    });
            });

            return new SmartContractVmBuilder(fullNodeBuilder);
        }

        public static IFullNodeBuilder UseSmartContractConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            LoggingConfiguration.RegisterFeatureClass<ConsensusStats>("bench");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ConsensusFeature>()
                .DependOn<SmartContractFeature>()
                .FeatureServices(services =>
                {
                    fullNodeBuilder.Network.Consensus.Options = new PowConsensusOptions();

                    services.AddSingleton<ICheckpoints, Checkpoints>();
                    services.AddSingleton<NBitcoin.Consensus.ConsensusOptions, PowConsensusOptions>();
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<CoinView, CachedCoinView>();
                    services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>()); ;
                    services.AddSingleton<IConsensusLoop, ConsensusLoop>()
                        .AddSingleton<INetworkDifficulty, ConsensusLoop>(provider => provider.GetService<IConsensusLoop>() as ConsensusLoop)
                        .AddSingleton<IGetUnspentTransaction, ConsensusLoop>(provider => provider.GetService<IConsensusLoop>() as ConsensusLoop);
                    services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<ConsensusStats>();
                    services.AddSingleton<ConsensusSettings>();

                    services.AddSingleton<IConsensusRules, SmartContractConsensusRules>();
                    services.AddSingleton<IRuleRegistration, SmartContractRuleRegistration>();
                });
            });

            return fullNodeBuilder;
        }
    }
}