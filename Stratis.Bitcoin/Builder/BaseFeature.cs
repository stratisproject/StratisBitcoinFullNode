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
		private List<IDisposable> _disposableResources = new List<IDisposable>();

		private NodeSettings _nodeSettings;
		private DataFolder _dataFolder;
		private Network _network;
		private FullNode _fullNodeInstance;
		private FullNode.CancellationProvider _cancellationProvider;
		private DateTimeProvider _dateTimeProvider;
		private ConcurrentChain _chain;
		private BlockStore.ChainBehavior.ChainState _chainBehaviorState;
		private Signals _signals;
		private ConnectionManager _connectionManager;


		private PeriodicTask _flushChainTask;

		private PeriodicTask _flushAddressManagerTask;

		public BaseFeature(
			NodeSettings nodeSettings, //node settings
			DataFolder dataFolder, //data folders
			Network network, //network (regtest/testnet/default)
			FullNode fullNodeInstance, //node instance
			FullNode.CancellationProvider cancellationProvider, //trigger when to dispose resources because of a global cancellation
			DateTimeProvider dateTimeProvider,
			ConcurrentChain chain,
			BlockStore.ChainBehavior.ChainState chainState,
			Signals signals, //event aggregator
			ConnectionManager connectionManager
			)
		{
			this._nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
			this._dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
			this._network = Guard.NotNull(network, nameof(network));
			this._fullNodeInstance = Guard.NotNull(fullNodeInstance, nameof(fullNodeInstance));
			this._cancellationProvider = Guard.NotNull(cancellationProvider, nameof(cancellationProvider));
			this._dateTimeProvider = Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
			this._chain = Guard.NotNull(chain, nameof(chain));
			this._chainBehaviorState = Guard.NotNull(chainState, nameof(chainState));
			this._signals = Guard.NotNull(signals, nameof(signals));
			this._connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
		}

		public override void Start()
		{
			StartConnectionManager();

			StartAddressManager();
			StartChain();
		}

		private void StartConnectionManager()
		{
			var connectionParameters = _connectionManager.Parameters;
			connectionParameters.IsRelay = _nodeSettings.Mempool.RelayTxes;
			connectionParameters.Services = (_nodeSettings.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

			_connectionManager = AutoDispose(new ConnectionManager(_network, connectionParameters, _nodeSettings));
		}

		private void StartChain()
		{
			if (!Directory.Exists(_dataFolder.ChainPath))
			{
				Logs.FullNode.LogInformation("Creating " + _dataFolder.ChainPath);
				Directory.CreateDirectory(_dataFolder.ChainPath);
			}

			var chainRepository = AutoDispose(new ChainRepository(_dataFolder.ChainPath));

			Logs.FullNode.LogInformation("Loading chain");

			chainRepository.Load(_chain).GetAwaiter().GetResult();

			Logs.FullNode.LogInformation("Chain loaded at height " + _chain.Height);

			_flushChainTask = new PeriodicTask("FlushChain", (cancellation) =>
			{
				chainRepository.Save(_chain);
			})
			.Start(_cancellationProvider.Cancellation.Token, TimeSpan.FromMinutes(5.0), true);

			AddNodeBehavior(new BlockStore.ChainBehavior(_chain, _chainBehaviorState));
		}

		private void StartAddressManager()
		{
			AddressManager addressManager;
			if (!File.Exists(_dataFolder.AddrManFile))
			{
				Logs.FullNode.LogInformation($"Creating {_dataFolder.AddrManFile}");
				addressManager = new AddressManager();
				addressManager.SavePeerFile(_dataFolder.AddrManFile, _network);
				Logs.FullNode.LogInformation("Created");
			}
			else
			{
				Logs.FullNode.LogInformation("Loading  {dataFolder.AddrManFile}");
				addressManager = AddressManager.LoadPeerFile(_dataFolder.AddrManFile);
				Logs.FullNode.LogInformation("Loaded");
			}

			if (addressManager.Count == 0)
			{
				Logs.FullNode.LogInformation("AddressManager is empty, discovering peers...");
			}

			_flushAddressManagerTask = new PeriodicTask("FlushAddressManager", (cancellation) =>
			{
				addressManager.SavePeerFile(_dataFolder.AddrManFile, _network);
			})
		   .Start(_cancellationProvider.Cancellation.Token, TimeSpan.FromMinutes(5.0), true);

			AddNodeBehavior(new AddressManagerBehavior(addressManager));
		}

		public override void Stop()
		{
			_cancellationProvider.Cancellation.Cancel();

			Logs.FullNode.LogInformation("FlushAddressManager stopped");
			_flushAddressManagerTask?.RunOnce();


			Logs.FullNode.LogInformation("FlushChain stopped");
			_flushChainTask?.RunOnce();

			foreach (var disposable in _disposableResources)
			{
				disposable.Dispose();
			}
		}

		#region Helpers
		private void AddNodeBehavior(INodeBehavior behavior)
		{
			_connectionManager.Parameters.TemplateBehaviors.Add(behavior);
		}

		public TDisposable AutoDispose<TDisposable>(TDisposable resource) where TDisposable : IDisposable
		{
			_disposableResources.Add(resource);
			return resource;
		}
		#endregion
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
					var nodeSettings = fullNodeBuilder.NodeSettings;
					var network = fullNodeBuilder.Network;

					services.AddSingleton<DataFolder>(serviceProvider => new DataFolder(nodeSettings));
					services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
					services.AddSingleton<FullNodeFeatureExecutor>();
					services.AddSingleton<FullNode>();
					services.AddSingleton<Signals>();
					services.AddSingleton<ConcurrentChain>(serviceProvider => new ConcurrentChain(network));
					services.AddSingleton(DateTimeProvider.Default);
					services.AddSingleton<BlockStore.ChainBehavior.ChainState>();
					services.AddSingleton(serviceProvider => new FullNode.CancellationProvider() { Cancellation = new CancellationTokenSource() });
					services.AddSingleton<ConnectionManager>(serviceProvider => new ConnectionManager(network, new NodeConnectionParameters(), nodeSettings));
				});
			});

			return fullNodeBuilder;
		}
	}
}