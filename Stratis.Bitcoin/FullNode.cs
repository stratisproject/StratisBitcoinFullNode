using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Miner;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{

	public class FullNode : IFullNode, IDisposable
	{
		private ApplicationLifetime applicationLifetime; // this will replace the cancellation token on the full node
		ILogger _logger;
		private FullNodeFeatureExecutor fullNodeFeatureExecutor;

		public FullNodeServiceProvider Services { get; set; }

		NodeSettings _Settings;

		public NodeSettings Settings
		{
			get { return _Settings; }
		}

        public FullNode()
        {
            _logger = Logs.LoggerFactory.CreateLogger<FullNode>();
        }

        public FullNode Initialize(FullNodeServiceProvider serviceProvider)
		{
			Guard.NotNull(serviceProvider, nameof(serviceProvider));

			this.Services = serviceProvider;

			this.DataFolder = this.Services.ServiceProvider.GetService<DataFolder>();
			this.DateTimeProvider = this.Services.ServiceProvider.GetService<IDateTimeProvider>();
			this.Network = this.Services.ServiceProvider.GetService<Network>();
			this._Settings = this.Services.ServiceProvider.GetService<NodeSettings>();
			this._ChainBehaviorState = this.Services.ServiceProvider.GetService<BlockStore.ChainBehavior.ChainState>();
			this.CoinView = this.Services.ServiceProvider.GetService<CoinView>();
			this.Chain = this.Services.ServiceProvider.GetService<ConcurrentChain>();
			this.GlobalCancellation = this.Services.ServiceProvider.GetService<CancellationProvider>();
			this.MempoolManager = this.Services.ServiceProvider.GetService<MempoolManager>();
			this.Signals = this.Services.ServiceProvider.GetService<Signals>();

			this.ConnectionManager = this.Services.ServiceProvider.GetService<ConnectionManager>();
			this.BlockStoreManager = this.Services.ServiceProvider.GetService<BlockStoreManager>();
			this.ConsensusLoop = this.Services.ServiceProvider.GetService<ConsensusLoop>();
			this.Miner = this.Services.ServiceProvider.GetService<Mining>();

            _logger.LogDebug("Full node initialized on {0}", Network.Name);

            return this;
		}

		protected void StartFeatures()
		{
			this.applicationLifetime = this.Services?.ServiceProvider.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
			this.fullNodeFeatureExecutor = this.Services?.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

			// Fire IApplicationLifetime.Started
			this.applicationLifetime?.NotifyStarted();

			//start all registered features
			this.fullNodeFeatureExecutor?.Start();
		}

		protected void DisposeFeatures()
		{
			// Fire IApplicationLifetime.Stopping
			this.applicationLifetime?.StopApplication();
			// Fire the IHostedService.Stop
			this.fullNodeFeatureExecutor?.Stop();
			(this.Services.ServiceProvider as IDisposable)?.Dispose();
			//(this.Services.ServiceProvider as IDisposable)?.Dispose();
			// Fire IApplicationLifetime.Stopped
			this.applicationLifetime?.NotifyStopped();
		}

		public Network Network
		{
			get;
			internal set;
		}

		public CoinView CoinView
		{
			get; set;
		}

		public DataFolder DataFolder
		{
			get; set;
		}

		public IDateTimeProvider DateTimeProvider
		{
			get; set;
		}

		public bool IsInitialBlockDownload()
		{
			//if (fImporting || fReindex)
			//	return true;
			if (this.ConsensusLoop.Tip == null)
				return true;
			if (this.ConsensusLoop.Tip.ChainWork < this.Network.Consensus.MinimumChainWork)
				return true;
			if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.Settings.MaxTipAge))
				return true;
			return false;
		}

		List<IDisposable> _Resources = new List<IDisposable>();
		public List<IDisposable> Resources => _Resources;

		public void Start()
		{
			if (IsDisposed)
				throw new ObjectDisposedException("FullNode");
			_IsStarted.Reset();

			// start all the features defined
			this.StartFeatures();
			
			ConnectionManager.Start();
			_IsStarted.Set();

			this.StartPeriodicLog();
		}

		private BlockStore.ChainBehavior.ChainState _ChainBehaviorState;
		public BlockStore.ChainBehavior.ChainState ChainBehaviorState
		{
			get { return _ChainBehaviorState; }
		}

		public Mining Miner
		{
			get; set;
		}

		public Signals Signals
		{
			get; set;
		}

		public ConsensusLoop ConsensusLoop
		{
			get; set;
		}

		public IWebHost RPCHost
		{
			get; set;
		}

		public ConnectionManager ConnectionManager
		{
			get; set;
		}

		public MempoolManager MempoolManager
		{
			get; set;
		}

		public ChainRepository ChainRepository
		{
			get; set;
		}

		public BlockStoreManager BlockStoreManager
		{
			get; set;
		}

		/// <summary>
		/// The longest PoW chain
		/// </summary>
		public ConcurrentChain Chain
		{
			get; set;
		}

		public PeriodicTask FlushAddrmanTask
		{
			get; set;
		}

		public PeriodicTask FlushChainTask
		{
			get; set;
		}

		public CancellationProvider GlobalCancellation
		{
			get; set;
		}
		public class CancellationProvider
		{
			public CancellationTokenSource Cancellation { get; set; }
		}

		ManualResetEvent _IsDisposed = new ManualResetEvent(false);
		ManualResetEvent _IsStarted = new ManualResetEvent(false);
		public bool IsDisposed
		{
			get
			{
				return _IsDisposedValue;
			}
		}

		private void StartPeriodicLog()
		{
			AsyncLoop.Run("PeriodicLog", (cancellation) =>
			{
				// TODO: move stats to each of its components
				StringBuilder benchLogs = new StringBuilder();

				if (this.ConsensusLoop != null)
				{
					benchLogs.AppendLine("======Consensus====== " + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
					benchLogs.AppendLine("Consensus.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.Height.ToString().PadRight(8) + " Consensus.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.HashBlock);
				}

				benchLogs.AppendLine("Headers.Height: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.Height.ToString().PadRight(8) + " Headers.Hash: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.HashBlock);

				if (this._ChainBehaviorState.HighestPersistedBlock != null)
				{
					benchLogs.AppendLine("Store.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.Height.ToString().PadRight(8) + " Store.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.HashBlock);
				}
				
				benchLogs.AppendLine();

				if (this.MempoolManager != null)
				{
					benchLogs.AppendLine("======Mempool======");
					benchLogs.AppendLine(this.MempoolManager.PerformanceCounter.ToString());
				}

				benchLogs.AppendLine("======Connection======");
				benchLogs.AppendLine(this.ConnectionManager.GetNodeStats());
				Logs.Bench.LogInformation(benchLogs.ToString());
				return Task.CompletedTask;
			},
			this.GlobalCancellation.Cancellation.Token,
			repeatEvery: TimeSpans.FiveSeconds,
			startAfter: TimeSpans.FiveSeconds);
		}

		public void WaitDisposed()
		{
			_IsDisposed.WaitOne();
			Dispose();
		}

		bool _IsDisposedValue;


		private bool _HasExited;
		private Exception _UncatchedException;

		public bool HasExited
		{
			get
			{
				return _HasExited;
			}
		}

		public void Dispose()
		{
			if (IsDisposed)
				return;
			_IsDisposedValue = true;
			Logs.FullNode.LogInformation("Closing node pending...");
			_IsStarted.WaitOne();
			if (this.GlobalCancellation != null)
			{
				this.GlobalCancellation.Cancellation.Cancel();

				ConnectionManager.Dispose();
				foreach (var dispo in _Resources)
					dispo.Dispose();

				DisposeFeatures();
			}
			_IsDisposed.Set();
			_HasExited = true;
		}

		public void ThrowIfUncatchedException()
		{
			if (_UncatchedException != null)
			{
				var ex = _UncatchedException;
				var aex = _UncatchedException as AggregateException;
				if (aex != null)
					ex = aex.InnerException;
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
		}
	}
}
