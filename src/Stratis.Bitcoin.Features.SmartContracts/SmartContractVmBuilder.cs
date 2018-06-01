using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractVmBuilder : ISmartContractVmBuilder
    {
        private readonly IFullNodeBuilder builder;

        public SmartContractVmBuilder(IFullNodeBuilder builder)
        {
            this.builder = builder;
        }

        public IFullNodeBuilder UseReflectionExecutor()
        {
            this.builder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ReflectionVirtualMachineFeature>()
                    .FeatureServices(services =>
                    {
                        SmartContractValidator validator = new SmartContractValidator(new List<ISmartContractValidator>
                        {
                            new SmartContractFormatValidator(ReferencedAssemblyResolver.AllowedAssemblies),
                            new SmartContractDeterminismValidator()
                        });
                        services.AddSingleton<SmartContractValidator>(validator);

                        services.AddSingleton<IKeyEncodingStrategy, BasicKeyEncodingStrategy>();
                        services.AddSingleton<ISmartContractExecutorFactory, ReflectionSmartContractExecutorFactory>();
                        services.AddSingleton<IMethodParameterSerializer, MethodParameterSerializer>();
                        // Add controller
                        services.AddSingleton<SmartContractsController>();
                        // Add rules
                        services.AddConsensusRules(new ReflectionRuleRegistration());
                    });
            });
            return this.builder;
        }

        public IFullNodeBuilder UseAnotherExecutor()
        {
            throw new System.NotImplementedException();
        }
    }
}
