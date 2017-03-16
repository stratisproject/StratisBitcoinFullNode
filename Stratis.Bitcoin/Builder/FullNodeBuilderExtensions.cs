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
		public static IFullNodeBuilder UseNodeArgs(this IFullNodeBuilder builder, NodeArgs nodeArgs)
		{
			var nodeBuilder = builder as FullNodeBuilder;
			nodeBuilder.NodeArgs = nodeArgs;
			nodeBuilder.Network = nodeArgs.GetNetwork();

			builder.ConfigureServices(service =>
			{
				service.AddSingleton(nodeBuilder.NodeArgs);
				service.AddSingleton(nodeBuilder.Network);
			});

			// the required services can be added once the args are set
			return builder.AddRequired();
		}

		public static IFullNodeBuilder UseDefaultNodeArgs(this IFullNodeBuilder builder)
		{
			return builder.UseNodeArgs(NodeArgs.Default());
		}

		public static IFullNodeBuilder AddRequired(this IFullNodeBuilder builder)
		{
			// TODO: move some of the required services will move to their own feature

			return builder.ConfigureServices(service =>
			{
				builder.UseBaseFeature();


				var dataFolder = new DataFolder(builder.NodeArgs);

				// TODO: move to ConsensusFeature (required for mempool)
				var coinviewdb = new DBreezeCoinView(builder.Network, dataFolder.CoinViewPath);
				var coinView = new CachedCoinView(coinviewdb) { MaxItems = builder.NodeArgs.Cache.MaxItems };
				var consensusValidator = new ConsensusValidator(builder.Network.Consensus);
				service.AddSingleton(consensusValidator);
				service.AddSingleton<DBreezeCoinView>(coinviewdb);
				service.AddSingleton<CoinView>(coinView);
				service.AddSingleton<Signals>();
			});

		}
	}
}