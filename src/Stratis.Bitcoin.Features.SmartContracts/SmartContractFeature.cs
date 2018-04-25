using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts.Controllers;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Serialization;
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
            this.stateRoot.SyncToRoot(this.consensusLoop.Chain.Tip.Header.HashStateRoot.ToBytes());
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
        public static IFullNodeBuilder AddSmartContracts(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SmartContractFeature>()
                    .DependOn<ConsensusFeature>()
                    .DependOn<MiningFeature>()
                    .FeatureServices(services =>
                    {
                        // Contract state
                        services.AddSingleton<DBreezeContractStateStore>();
                        services.AddSingleton<NoDeleteContractStateSource>();
                        services.AddSingleton<ContractStateRepositoryRoot>();
                        // Consensus and mining overrides
                        services.AddSingleton<IPowConsensusValidator, SmartContractConsensusValidator>();
                        services.AddSingleton<IAssemblerFactory, SmartContractAssemblerFactory>();
                        // Controller for API
                        services.AddSingleton<SmartContractsController>();
                        // Add rules -> These could be VM specific in future though!
                        AddSmartContractRulesToExistingRules(services);
                    });
            });
            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseReflectionVirtualMachine(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ReflectionVirtualMachineFeature>()
                    .FeatureServices(services =>
                    {
                        SmartContractValidator validator = new SmartContractValidator(new List<ISmartContractValidator>
                        {
                            new SmartContractFormatValidator(),
                            new SmartContractDeterminismValidator()
                        });
                        services.AddSingleton<SmartContractValidator>(validator);
                        services.AddSingleton<IKeyEncodingStrategy, BasicKeyEncodingStrategy>();
                        services.AddSingleton<ISmartContractExecutorFactory, ReflectionSmartContractExecutorFactory>();
                        services.AddSingleton<IMethodParameterSerializer, MethodParameterSerializer>();
                        services.AddSingleton<ISmartContractCarrierSerializer, SmartContractCarrierSerializer>();
                    });
            });
            return fullNodeBuilder;
        }

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
        /// <param name="services"></param>
        private static void AddSmartContractRulesToExistingRules(IServiceCollection services)
        {
            ServiceDescriptor existingService = services.FirstOrDefault(s => s.ServiceType == typeof(IRuleRegistration));

            if (existingService == null)
            {
                // This should never happen
                throw new Exception("SmartContracts feature must be added after Consensus feature");
            }

            Type concreteType = existingService.ImplementationType;

            // Register concrete type if it does not already exist
            if (services.FirstOrDefault(s => s.ServiceType == concreteType) == null)
            {
                services.Add(new ServiceDescriptor(concreteType, concreteType, ServiceLifetime.Singleton));
            }

            // Replace the existing rule registration with our own factory
            var newService = new ServiceDescriptor(typeof(IRuleRegistration), serviceProvider =>
            {
                var existingRuleRegistration = serviceProvider.GetService(concreteType);
                return new SmartContractRuleRegistration((IRuleRegistration)existingRuleRegistration);
            }, ServiceLifetime.Singleton);

            services.Replace(newService);
        }
    }
}