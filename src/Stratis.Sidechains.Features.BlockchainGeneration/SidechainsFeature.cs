using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
	public class SidechainsFeature : FullNodeFeature
	{
		public SidechainsFeature()
		{
			
		}

        public override void Initialize()
        {
            //do nothing
        }
	}

	public static partial class IFullNodeBuilderExtensions
	{
		public static IFullNodeBuilder UseSidechains(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<SidechainsFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<ISidechainsManager, SidechainsManager>();
					    services.AddSingleton<SidechainsController>();
                    });
			});
			return fullNodeBuilder;
		}
	}
}
