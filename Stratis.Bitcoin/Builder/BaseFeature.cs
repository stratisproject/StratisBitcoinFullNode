using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Builder.Feature;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using ChainBehavior = Stratis.Bitcoin.BlockStore.ChainBehavior;

namespace Stratis.Bitcoin.Builder
{
	/// <summary>
	/// Base node services, this are the services a node has to have
	/// the ConnectionManagerFeature is also part of the base but may go in a feature of its own
	/// the base features are the minimal components required to connect to peers and maintain the best chain
	/// the base node services for a node are: 
	/// - the ConcurrentChain to keep track of the best chain 
	/// - the ConnectionManager to connect with the network
	/// - DatetimeProvider and Cancellation
	/// - CancellationProvider and Cancellation
	/// - DataFolder 
	/// - ChainState
	/// </summary>
	public class BaseFeature : FullNodeFeature
	{
		/// <summary>
		/// disposable resources that will be disposed when the feature stops
		/// </summary>
		private readonly List<IDisposable> disposableResources = new List<IDisposable>();

		private readonly ChainBehavior.ChainState chainState;
		private readonly ChainRepository chainRepository;
		private readonly NodeSettings nodeSettings;
		private readonly DataFolder dataFolder;
		private readonly Network network;
		private readonly FullNode.CancellationProvider cancellationProvider;
		private readonly ConcurrentChain chain;
		private readonly ConnectionManager connectionManager;

		private PeriodicTask flushChainTask;
		private PeriodicTask flushAddressManagerTask;

		private AddressManager addressManager;

		public BaseFeature(
			NodeSettings nodeSettings, //node settings
			DataFolder dataFolder, //data folders
			Network network, //network (regtest/testnet/default)
			FullNode.CancellationProvider cancellationProvider, //trigger when to dispose resources because of a global cancellation
			ConcurrentChain chain,
			BlockStore.ChainBehavior.ChainState chainState,
			ConnectionManager connectionManager,
			ChainRepository chainRepository)
		{
			this.chainState = chainState;
			this.chainRepository = chainRepository;
			this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
			this.dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
			this.network = Guard.NotNull(network, nameof(network));
			this.cancellationProvider = Guard.NotNull(cancellationProvider, nameof(cancellationProvider));
			this.chain = Guard.NotNull(chain, nameof(chain));
			this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
		}
	
		public override void Start()
		{
			StartAddressManager();
			StartChain();

			var connectionParameters = connectionManager.Parameters;
			connectionParameters.IsRelay = nodeSettings.Mempool.RelayTxes;
			connectionParameters.Services = (nodeSettings.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

			this.connectionManager.Parameters.TemplateBehaviors.Add(new ChainBehavior(this.chain, this.chainState));
			this.connectionManager.Parameters.TemplateBehaviors.Add(new AddressManagerBehavior(this.addressManager));

			this.disposableResources.Add(this.chainRepository);
			this.disposableResources.Add(this.connectionManager);
		}

		private void StartChain()
		{
			if (!Directory.Exists(dataFolder.ChainPath))
			{
				Logs.FullNode.LogInformation("Creating " + dataFolder.ChainPath);
				Directory.CreateDirectory(dataFolder.ChainPath);
			}

			Logs.FullNode.LogInformation("Loading chain");
			chainRepository.Load(chain).GetAwaiter().GetResult();

			Logs.FullNode.LogInformation("Chain loaded at height " + chain.Height);
			flushChainTask = new PeriodicTask("FlushChain", (cancellation) =>
			{
				chainRepository.Save(chain);
			})
			.Start(cancellationProvider.Cancellation.Token, TimeSpan.FromMinutes(5.0), true);
		}

		private void StartAddressManager()
		{
			if (!File.Exists(dataFolder.AddrManFile))
			{
				Logs.FullNode.LogInformation($"Creating {dataFolder.AddrManFile}");
				addressManager = new AddressManager();
				addressManager.SavePeerFile(dataFolder.AddrManFile, network);
				Logs.FullNode.LogInformation("Created");
			}
			else
			{
				Logs.FullNode.LogInformation("Loading  {dataFolder.AddrManFile}");
				addressManager = AddressManager.LoadPeerFile(dataFolder.AddrManFile);
				Logs.FullNode.LogInformation("Loaded");
			}

			if (addressManager.Count == 0)
			{
				Logs.FullNode.LogInformation("AddressManager is empty, discovering peers...");
			}

			flushAddressManagerTask = new PeriodicTask("FlushAddressManager", (cancellation) =>
			{
				addressManager.SavePeerFile(dataFolder.AddrManFile, network);
			})
		   .Start(cancellationProvider.Cancellation.Token, TimeSpan.FromMinutes(5.0), true);
		}

		public override void Stop()
		{
			Logs.FullNode.LogInformation("FlushAddressManager stopped");
			flushAddressManagerTask?.RunOnce();

			Logs.FullNode.LogInformation("FlushChain stopped");
			flushChainTask?.RunOnce();

			foreach (var disposable in disposableResources)
			{
				disposable.Dispose();
			}
		}
	}

	internal static class BaseFeatureBuilderExtension
	{
		public static IFullNodeBuilder UseBaseFeature(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
				.AddFeature<BaseFeature>()
				.FeatureServices(services =>
				{
					services.AddSingleton<DataFolder>();
					services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
					services.AddSingleton<FullNodeFeatureExecutor>();
					services.AddSingleton<FullNode>();
					services.AddSingleton<Signals>();
					services.AddSingleton<ConcurrentChain>(new ConcurrentChain(fullNodeBuilder.Network));
					services.AddSingleton(DateTimeProvider.Default);
					services.AddSingleton<BlockStore.ChainBehavior.ChainState>();
					services.AddSingleton<ChainRepository>();
					services.AddSingleton(serviceProvider => new FullNode.CancellationProvider() { Cancellation = new CancellationTokenSource() });

					// == connection ==
					services.AddSingleton<NodeConnectionParameters>(new NodeConnectionParameters());
					services.AddSingleton<ConnectionManager>();
				});
			});

			return fullNodeBuilder;
		}
	}
}