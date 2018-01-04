using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Consensus;

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
                        services.AddSingleton<PowConsensusValidator, SCConsensusValidator>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
