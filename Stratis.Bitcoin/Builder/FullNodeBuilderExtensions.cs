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
				var nodeBuilder = builder as FullNodeBuilder;

				// Base node services, this are the services a node has to have
				// the ConnectionManagerFeature is also part of the base but may go in a feature of its own
				// the base features are the minimal components required to connect to peers and maintain the best chain
				// the base node services for a node are: 
				// - the ConcurrentChain to keep track of the best chain 
				// - the ConnectionManager to connect with the network
				// - DatetimeProvider and Cancellation
				// - CancellationProvider and Cancellation
				// - DataFolder 
				// - ChainState

				// TODO: move to NodeBaseFeature (or a RequiredFeature)
				service.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
				service.AddSingleton<FullNodeFeatureExecutor>();
				service.AddSingleton<FullNode>();
				service.AddSingleton(new ConcurrentChain(nodeBuilder.Network));
				service.AddSingleton(DateTimeProvider.Default);
				var dataFolder = new DataFolder(nodeBuilder.NodeArgs);
				service.AddSingleton(dataFolder);
				var cancellation = new CancellationTokenSource();
				var cancellationProvider = new FullNode.CancellationProvider() { Cancellation = cancellation };
				service.AddSingleton(cancellationProvider);
				service.AddSingleton<BlockStore.ChainBehavior.ChainState>();

				// TODO: move to ConsensusFeature (required for mempool)
				var coinviewdb = new DBreezeCoinView(nodeBuilder.Network, dataFolder.CoinViewPath);
				var coinView = new CachedCoinView(coinviewdb) {MaxItems = nodeBuilder.NodeArgs.Cache.MaxItems};
				var consensusValidator = new ConsensusValidator(nodeBuilder.Network.Consensus);
				service.AddSingleton(consensusValidator);
				service.AddSingleton<DBreezeCoinView>(coinviewdb);
				service.AddSingleton<CoinView>(coinView);

				// TODO: move to ConnectionManagerFeature
				var connectionManager = new ConnectionManager(nodeBuilder.Network, new NodeConnectionParameters(),
					nodeBuilder.NodeArgs.ConnectionManager);
				service.AddSingleton(connectionManager);
			});

		}
	}
}