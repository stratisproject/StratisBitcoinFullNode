using System;
using System.Collections.Generic;
using System.Linq;
using DBreeze;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Miner;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractFeature : FullNodeFeature
    {
        private readonly ILogger logger;

        public SmartContractFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("Smart Contract Feature Injected.");
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
                        services.AddSingleton<SmartContractDecompiler>();
                        SmartContractValidator validator = new SmartContractValidator(new List<ISmartContractValidator>
                        {
                            new SmartContractFormatValidator(),
                            new SmartContractDeterminismValidator()
                        });
                        services.AddSingleton<SmartContractValidator>(validator);
                        services.AddSingleton<SmartContractGasInjector>();

                        // TODO: Get root from somewhere and get these strings from somewhere
                        //DBreezeEngine engine = new DBreezeEngine("C:/data");
                        //DBreezeByteStore byteStore = new DBreezeByteStore(engine, "ContractState");

                        //  TODO: For testing, we use in-memory database for now. Real life needs to use dbreeze.

                        MemoryDictionarySource byteStore = new MemoryDictionarySource();

                        ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);
                        byte[] root = null;

                        ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(stateDB, root);
                        services.AddSingleton<IContractStateRepository>(repository);
                        services.AddSingleton<PowConsensusValidator, SmartContractConsensusValidator>();
                        services.AddSingleton<IAssemblerFactory, SmartContractAssemblerFactory>();

                        AddSmartContractRulesToExistingRules(services);
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
