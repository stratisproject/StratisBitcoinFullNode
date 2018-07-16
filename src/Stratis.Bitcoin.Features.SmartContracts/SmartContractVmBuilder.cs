using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractVmBuilder : ISmartContractVmBuilder
    {
        private readonly IFullNodeBuilder fullNodeBuilder;

        public SmartContractVmBuilder(IFullNodeBuilder fullNodeBuilder)
        {
            this.fullNodeBuilder = fullNodeBuilder;
        }

        public IFullNodeBuilder UseReflectionExecutor()
        {
            this.fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ReflectionVirtualMachineFeature>()
                    .FeatureServices(services =>
                    {
                        // Validator
                        SmartContractValidator validator = new SmartContractValidator(new List<ISmartContractValidator>
                        {
                            new SmartContractFormatValidator(ReferencedAssemblyResolver.AllowedAssemblies),
                            new SmartContractDeterminismValidator()
                        });
                        services.AddSingleton(validator);

                        // Executor et al.
                        services.AddSingleton<ISmartContractResultRefundProcessor, SmartContractResultRefundProcessor>();
                        services.AddSingleton<ISmartContractResultTransferProcessor, SmartContractResultTransferProcessor>();
                        services.AddSingleton<IKeyEncodingStrategy, BasicKeyEncodingStrategy>();
                        services.AddSingleton<ISmartContractExecutorFactory, ReflectionSmartContractExecutorFactory>();
                        services.AddSingleton<IMethodParameterSerializer, MethodParameterSerializer>();

                        // Controllers
                        services.AddSingleton<SmartContractsController>();
                    });
            });

            return this.fullNodeBuilder;
        }

        public IFullNodeBuilder UseAnotherExecutor()
        {
            throw new NotImplementedException();
        }
    }
}