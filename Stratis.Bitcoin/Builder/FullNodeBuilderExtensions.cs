using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Builder
{
	public static class FullNodeBuilderExtensions
	{
		public static IFullNodeBuilder UseNodeSettings(this IFullNodeBuilder builder, NodeSettings nodeSettings)
		{
			var nodeBuilder = builder as FullNodeBuilder;
			nodeBuilder.NodeSettings = nodeSettings;
			nodeBuilder.Network = nodeSettings.GetNetwork();

			builder.ConfigureServices(service =>
			{
				service.AddSingleton(nodeBuilder.NodeSettings);
				service.AddSingleton(nodeBuilder.Network);
			});

			// the required services can be added once the args are set
			return builder.AddRequired();
		}

		public static IFullNodeBuilder UseDefaultNodeSettings(this IFullNodeBuilder builder)
		{
			return builder.UseNodeSettings(NodeSettings.Default());
		}

		public static IFullNodeBuilder AddRequired(this IFullNodeBuilder builder)
		{
			builder.UseBaseFeature();

			// TODO: move some of the required services will move to their own feature
			return builder.ConfigureServices(service =>
			{


				var dataFolder = new DataFolder(builder.NodeSettings);

				// TODO: move to ConsensusFeature (required for mempool)
				var coinviewdb = new DBreezeCoinView(builder.Network, dataFolder.CoinViewPath);
				var coinView = new CachedCoinView(coinviewdb) { MaxItems = builder.NodeSettings.Cache.MaxItems };
				var consensusValidator = new ConsensusValidator(builder.Network.Consensus);
				service.AddSingleton(consensusValidator);
				service.AddSingleton<DBreezeCoinView>(coinviewdb);
				service.AddSingleton<CoinView>(coinView);
			});

		}
	}
}