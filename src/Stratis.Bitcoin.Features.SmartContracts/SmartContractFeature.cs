using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly ContractStateRepositoryRoot stateRoot;
        private readonly IConsensusLoop consensusLoop;

        public SmartContractFeature(ILoggerFactory loggerFactory, ContractStateRepositoryRoot stateRoot, IConsensusLoop consensusLoop)
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

    public class ReflectionVirtualMachineFeature : FullNodeFeature
    {
        private readonly ILogger logger;

        public ReflectionVirtualMachineFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("Reflection Virtual Machine Injected.");
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
                    .DependOn<ConsensusFeature>()
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
                        services.AddConsensusRules(new SmartContractRuleRegistration(fullNodeBuilder));
                    });
            });
            return new SmartContractVmBuilder(fullNodeBuilder);
        }
    }

    public static class ConsensusRuleUtils
    {
        /// <summary>
        /// This is a hack to enable us to to compose objects that depend on implementations defined earlier
        /// in the feature setup process.
        ///
        /// We want to be able to take any existing IRuleRegistration, and extend it with our own
        /// smart contract specific rules.
        ///
        /// Here we get an existing IRuleRegistration ServiceDescriptor, re-register it as its ConcreteType
        /// then replace the dependency on IRuleRegistration with our own implementation that depends on ConcreteType.
        /// </summary>
        public static void AddConsensusRules(this IServiceCollection services, IAdditionalRuleRegistration rulesToAdd)
        {
            ServiceDescriptor existingService = services.FirstOrDefault(s => s.ServiceType == typeof(IRuleRegistration));
            if (existingService == null)
                throw new Exception("SmartContracts feature must be added after Consensus feature");

            Type concreteType = existingService.ImplementationType;
            if (concreteType != null)
            {
                // Register concrete type if it does not already exist
                if (services.FirstOrDefault(s => s.ServiceType == concreteType) == null)
                    services.Add(new ServiceDescriptor(concreteType, concreteType, ServiceLifetime.Singleton));

                // Replace the existing rule registration with our own factory
                var newService = new ServiceDescriptor(typeof(IRuleRegistration), serviceProvider =>
                {
                    var existingRuleRegistration = serviceProvider.GetService(concreteType);
                    rulesToAdd.SetPreviousRegistration((IRuleRegistration)existingRuleRegistration);
                    return rulesToAdd;
                }, ServiceLifetime.Singleton);

                services.Replace(newService);

                return;
            }

            Func<IServiceProvider, object> implementationFactory = existingService.ImplementationFactory;

            if (implementationFactory != null)
            {
                // Factory method has already been defined, just add the extra rules
                var newService = new ServiceDescriptor(typeof(IRuleRegistration), serviceProvider =>
                {
                    var existingRuleRegistration = implementationFactory.Invoke(serviceProvider);
                    rulesToAdd.SetPreviousRegistration((IRuleRegistration)existingRuleRegistration);
                    return rulesToAdd;
                }, ServiceLifetime.Singleton);

                services.Replace(newService);
            }
        }
    }
}