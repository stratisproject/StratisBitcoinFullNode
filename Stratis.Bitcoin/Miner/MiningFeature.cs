using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using System;
using System.Linq;
using System.Threading;
using static Stratis.Bitcoin.FullNode;

namespace Stratis.Bitcoin.Miner
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

	public static class MiningFeatureExtension
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
