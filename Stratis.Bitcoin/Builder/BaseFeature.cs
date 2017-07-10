using System;
using System.Collections.Generic;
using System.IO;
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
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Consensus.Deployments;
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
	    private readonly INodeLifetime nodeLifetime;

	    /// <summary>
		/// disposable resources that will be disposed when the feature stops
		/// </summary>
		private readonly List<IDisposable> disposableResources = new List<IDisposable>();

		private readonly ChainBehavior.ChainState chainState;
		private readonly ChainRepository chainRepository;
		private readonly NodeSettings nodeSettings;
		private readonly DataFolder dataFolder;
		private readonly Network network;
		private readonly ConcurrentChain chain;
		private readonly IConnectionManager connectionManager;

		private PeriodicTask flushChainTask;
		private PeriodicTask flushAddressManagerTask;

		private AddressManager addressManager;
	    private readonly ILogger logger;

        public BaseFeature(
			NodeSettings nodeSettings, //node settings
			DataFolder dataFolder, //data folders
			Network network, //network (regtest/testnet/default)
			INodeLifetime nodeLifetime, //trigger when to dispose resources because of a global cancellation
			ConcurrentChain chain,
			BlockStore.ChainBehavior.ChainState chainState,
			IConnectionManager connectionManager,
			ChainRepository chainRepository,
            ILoggerFactory loggerFactory)
		{
		    this.chainState = Guard.NotNull(chainState, nameof(chainState));
			this.chainRepository = Guard.NotNull(chainRepository, nameof(chainRepository));
			this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
			this.dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
			this.network = Guard.NotNull(network, nameof(network));
			this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
			this.chain = Guard.NotNull(chain, nameof(chain));
			this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
		    this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }
	
		public override void Start()
		{
			this.StartAddressManager();
			this.StartChain();

			var connectionParameters = this.connectionManager.Parameters;
			connectionParameters.IsRelay = this.nodeSettings.Mempool.RelayTxes;
			connectionParameters.TemplateBehaviors.Add(new ChainBehavior(this.chain, this.chainState));
			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(this.addressManager));

		    this.disposableResources.Add(this.nodeSettings.LoggerFactory);
            this.disposableResources.Add(this.chainRepository);
			this.disposableResources.Add(this.connectionManager);
		}

		private void StartChain()
		{
			if (!Directory.Exists(this.dataFolder.ChainPath))
			{
				this.logger.LogInformation("Creating " + this.dataFolder.ChainPath);
				Directory.CreateDirectory(this.dataFolder.ChainPath);
			}

		    this.logger.LogInformation("Loading chain");
            this.chainRepository.Load(this.chain).GetAwaiter().GetResult();

		    this.logger.LogInformation("Chain loaded at height " + this.chain.Height);
            this.flushChainTask = new PeriodicTask("FlushChain", this.logger, (cancellation) =>
			{
                this.chainRepository.Save(this.chain);
			})
			.Start(this.nodeLifetime.ApplicationStopping, TimeSpan.FromMinutes(5.0), true);
		}

		private void StartAddressManager()
		{
			if (!File.Exists(this.dataFolder.AddrManFile))
			{
			    this.logger.LogInformation($"Creating {dataFolder.AddrManFile}");
				this.addressManager = new AddressManager();
                this.addressManager.SavePeerFile(this.dataFolder.AddrManFile, this.network);
			    this.logger.LogInformation("Created");
			}
			else
			{
			    this.logger.LogInformation($"Loading  {dataFolder.AddrManFile}");
                this.addressManager = AddressManager.LoadPeerFile(this.dataFolder.AddrManFile);
			    this.logger.LogInformation("Loaded");
			}

			if (this.addressManager.Count == 0)
			{
			    this.logger.LogInformation("AddressManager is empty, discovering peers...");
			}

            this.flushAddressManagerTask = new PeriodicTask("FlushAddressManager", this.logger, (cancellation) =>
			{
                this.addressManager.SavePeerFile(this.dataFolder.AddrManFile, this.network);
			})
		   .Start(this.nodeLifetime.ApplicationStopping, TimeSpan.FromMinutes(5.0), true);
		}

		public override void Stop()
		{
		    this.logger.LogInformation("FlushAddressManager stopped");
            this.flushAddressManagerTask?.RunOnce();

		    this.logger.LogInformation("FlushChain stopped");
            this.flushChainTask?.RunOnce();

			foreach (var disposable in this.disposableResources)
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
					services.AddSingleton(fullNodeBuilder.NodeSettings.LoggerFactory);
					services.AddSingleton(fullNodeBuilder.NodeSettings.DataFolder);
					services.AddSingleton<INodeLifetime, NodeLifetime>();
					services.AddSingleton<FullNodeFeatureExecutor>();
					services.AddSingleton<Signals>().AddSingleton<ISignals, Signals>(provider => provider.GetService<Signals>());          
					services.AddSingleton<FullNode>().AddSingleton((provider) => { return provider.GetService<FullNode>() as IFullNode; });
					services.AddSingleton<ConcurrentChain>(new ConcurrentChain(fullNodeBuilder.Network));
					services.AddSingleton<IDateTimeProvider>(DateTimeProvider.Default);
					services.AddSingleton<BlockStore.ChainBehavior.ChainState>();
					services.AddSingleton<ChainRepository>();
				    services.AddSingleton<IAsyncLoopFactory, AsyncLoopFactory>();
				    services.AddSingleton<NodeDeployments>();

                    // == connection ==
                    services.AddSingleton<NodeConnectionParameters>(new NodeConnectionParameters());
					services.AddSingleton<IConnectionManager, ConnectionManager>();
				});
			});

			return fullNodeBuilder;
		}
	}
}