using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoS;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Decompilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractFeature : FullNodeFeature
    {
        private readonly IConsensusManager consensusManager;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRoot;

        public SmartContractFeature(IConsensusManager consensusLoop, ILoggerFactory loggerFactory, Network network, IStateRepositoryRoot stateRoot)
        {
            this.consensusManager = consensusLoop;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.stateRoot = stateRoot;
        }

        public override Task InitializeAsync()
        {
            // TODO: This check should be more robust
            if (this.network.Consensus.IsProofOfStake)
                Guard.Assert(this.network.Consensus.ConsensusFactory is SmartContractPosConsensusFactory);
            else
                Guard.Assert(this.network.Consensus.ConsensusFactory is SmartContractPowConsensusFactory || this.network.Consensus.ConsensusFactory is SmartContractPoAConsensusFactory);

            this.stateRoot.SyncToRoot(((ISmartContractBlockHeader)this.consensusManager.Tip.Header).HashStateRoot.ToBytes());

            this.logger.LogInformation("Smart Contract Feature Injected.");
            return Task.CompletedTask;
        }
    }

    public static partial class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Adds the smart contract feature to the node.
        /// </summary>
        public static IFullNodeBuilder AddSmartContracts(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SmartContractFeature>("smartcontracts");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SmartContractFeature>()
                    .FeatureServices(services =>
                    {
                        // STATE ----------------------------------------------------------------------------
                        services.AddSingleton<DBreezeContractStateStore>();
                        services.AddSingleton<NoDeleteContractStateSource>();
                        services.AddSingleton<IStateRepositoryRoot, StateRepositoryRoot>();

                        // CONSENSUS ------------------------------------------------------------------------
                        services.AddSingleton<IMempoolValidator, SmartContractMempoolValidator>();
                        services.AddSingleton<StandardTransactionPolicy, SmartContractTransactionPolicy>();

                        // CONTRACT EXECUTION ---------------------------------------------------------------
                        services.AddSingleton<IInternalExecutorFactory, InternalExecutorFactory>();
                        services.AddSingleton<IVirtualMachine, ReflectionVirtualMachine>();
                        services.AddSingleton<IAddressGenerator, AddressGenerator>();
                        services.AddSingleton<ILoader, ContractAssemblyLoader>();
                        services.AddSingleton<IContractModuleDefinitionReader, ContractModuleDefinitionReader>();
                        services.AddSingleton<IStateFactory, StateFactory>();
                        services.AddSingleton<SmartContractTransactionPolicy>();
                        services.AddSingleton<IStateProcessor, StateProcessor>();
                        services.AddSingleton<ISmartContractStateFactory, SmartContractStateFactory>();
                        services.AddSingleton<ILocalExecutor, LocalExecutor>();

                        // RECEIPTS -------------------------------------------------------------------------
                        services.AddSingleton<IReceiptRepository, PersistentReceiptRepository>();

                        // UTILS ----------------------------------------------------------------------------
                        services.AddSingleton<ISenderRetriever, SenderRetriever>();
                        services.AddSingleton<IVersionProvider, SmartContractVersionProvider>();

                        services.AddSingleton<IMethodParameterSerializer, MethodParameterByteSerializer>();
                        services.AddSingleton<IMethodParameterStringSerializer, MethodParameterStringSerializer>();
                        services.AddSingleton<ICallDataSerializer, CallDataSerializer>();

                        // Registers the ScriptAddressReader concrete type and replaces the IScriptAddressReader implementation
                        // with SmartContractScriptAddressReader, which depends on the ScriptAddressReader concrete type.
                        services.AddSingleton<ScriptAddressReader>();
                        services.Replace(new ServiceDescriptor(typeof(IScriptAddressReader), typeof(SmartContractScriptAddressReader), ServiceLifetime.Singleton));
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds the smart contract feature to the node, also requiring executed contracts to be signed.
        /// </summary>
        public static IFullNodeBuilder AddSignedSmartContracts(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SmartContractFeature>("smartcontracts");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SmartContractFeature>()
                    .FeatureServices(services =>
                    {
                        // STATE ----------------------------------------------------------------------------
                        services.AddSingleton<DBreezeContractStateStore>();
                        services.AddSingleton<NoDeleteContractStateSource>();
                        services.AddSingleton<IStateRepositoryRoot, StateRepositoryRoot>();

                        // CONSENSUS ------------------------------------------------------------------------
                        services.AddSingleton<IMempoolValidator, SmartContractMempoolValidator>();
                        services.AddSingleton<StandardTransactionPolicy, SmartContractTransactionPolicy>();

                        // CONTRACT EXECUTION ---------------------------------------------------------------
                        services.AddSingleton<IInternalExecutorFactory, InternalExecutorFactory>();
                        services.AddSingleton<IVirtualMachine, ReflectionVirtualMachine>();
                        services.AddSingleton<IAddressGenerator, AddressGenerator>();
                        services.AddSingleton<ILoader, ContractAssemblyLoader>();
                        services.AddSingleton<IContractModuleDefinitionReader, ContractModuleDefinitionReader>();
                        services.AddSingleton<IStateFactory, StateFactory>();
                        services.AddSingleton<SmartContractTransactionPolicy>();
                        services.AddSingleton<IStateProcessor, StateProcessor>();
                        services.AddSingleton<ISmartContractStateFactory, SmartContractStateFactory>();
                        services.AddSingleton<ILocalExecutor, LocalExecutor>();

                        // RECEIPTS -------------------------------------------------------------------------
                        services.AddSingleton<IReceiptRepository, PersistentReceiptRepository>();

                        // UTILS ----------------------------------------------------------------------------
                        services.AddSingleton<ISenderRetriever, SenderRetriever>();
                        services.AddSingleton<IVersionProvider, SmartContractVersionProvider>();

                        services.AddSingleton<IMethodParameterSerializer, MethodParameterByteSerializer>();
                        services.AddSingleton<IMethodParameterStringSerializer, MethodParameterStringSerializer>();
                        services.AddSingleton<ICallDataSerializer, CallDataSerializer>();

                        // Registers the ScriptAddressReader concrete type and replaces the IScriptAddressReader implementation
                        // with SmartContractScriptAddressReader, which depends on the ScriptAddressReader concrete type.
                        services.AddSingleton<ScriptAddressReader>();
                        services.Replace(new ServiceDescriptor(typeof(IScriptAddressReader), typeof(SmartContractScriptAddressReader), ServiceLifetime.Singleton));
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// This node will be configured with the reflection contract executor.
        /// <para>
        /// Should we require another executor, we will need to create a separate daemon and network.
        /// </para>
        /// </summary>
        public static IFullNodeBuilder UseReflectionExecutor(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ReflectionVirtualMachineFeature>()
                    .FeatureServices(services =>
                    {
                        // Validator
                        services.AddSingleton<ISmartContractValidator, SmartContractValidator>();

                        // Executor et al.
                        services.AddSingleton<IContractRefundProcessor, ContractRefundProcessor>();
                        services.AddSingleton<IContractTransferProcessor, ContractTransferProcessor>();
                        services.AddSingleton<IKeyEncodingStrategy, BasicKeyEncodingStrategy>();
                        services.AddSingleton<IContractExecutorFactory, ReflectionExecutorFactory>();
                        services.AddSingleton<IMethodParameterSerializer, MethodParameterByteSerializer>();
                        services.AddSingleton<IContractPrimitiveSerializer, ContractPrimitiveSerializer>();
                        services.AddSingleton<ISerializer, Serializer>();

                        // Controllers + utils
                        services.AddSingleton<CSharpContractDecompiler>();
                        services.AddSingleton<SmartContractsController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}