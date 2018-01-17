using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.SmartContracts.ContractValidation;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractFeature : FullNodeFeature
    {
        public override void Initialize()
        {
            throw new NotImplementedException("At this point the feature is only used to inject the new consensus validator");
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
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<SmartContractDecompiler>();
                        SmartContractValidator validator = new SmartContractValidator(new List<ISmartContractValidator>
                        {
                            new SmartContractFormatValidator(),
                            new SmartContractDeterminismValidator()
                        });
                        // TODO: Add repository
                        services.AddSingleton<SmartContractValidator>(validator);
                        services.AddSingleton<SmartContractGasInjector>();
                        services.AddSingleton<PowConsensusValidator, SCConsensusValidator>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
