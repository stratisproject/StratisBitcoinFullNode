using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Sidechains.Features.BlockGenerator.Controllers;

namespace Stratis.Sidechains.Features.BlockGenerator
{
	public class BlockGeneratorFeature : FullNodeFeature
	{
		public BlockGeneratorFeature()
		{
		}

		public override void Start()
		{
		}
	}

	public static partial class IFullNodeBuilderExtensions
	{
		public static IFullNodeBuilder UseBlockGenerator(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<BlockGeneratorFeature>()
					.FeatureServices(services =>
					{
						//services.AddSingleton<ISidechainActor, SidechainActor>();
						services.AddSingleton<BlockGeneratorController>();
						services.AddSingleton<BlockManager>();

					});
			});
			return fullNodeBuilder;
		}
	}
}