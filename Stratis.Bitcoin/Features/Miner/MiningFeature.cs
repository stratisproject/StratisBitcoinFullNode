using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.Miner
{
	public class MiningFeature : FullNodeFeature
	{		
		public override void Start()
		{
		}

		public override void Stop()
		{
		}
	}

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
		public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<MiningFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<PowMining>();
						services.AddSingleton<AssemblerFactory, PowAssemblerFactory>();
					});
			});

			return fullNodeBuilder;
		}

		public static IFullNodeBuilder AddPowPosMining(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<MiningFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<PowMining>();
						services.AddSingleton<PosMinting>();
						services.AddSingleton<AssemblerFactory, PosAssemblerFactory>();
					});
			});

			return fullNodeBuilder;
		}
	}
}
