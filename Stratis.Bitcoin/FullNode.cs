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
using System.Reflection;

namespace Stratis.Bitcoin
{

	public class FullNode : IFullNode
	{
		private readonly ILogger logger;
		private ApplicationLifetime applicationLifetime;
		private FullNodeFeatureExecutor fullNodeFeatureExecutor;
		private readonly ManualResetEvent isDisposed;
		private readonly ManualResetEvent isStarted;

		internal bool Stopped;

		public FullNode()
		{
			this.logger = Logs.LoggerFactory.CreateLogger<FullNode>();
			this.isDisposed = new ManualResetEvent(false);
			this.isStarted = new ManualResetEvent(false);
			this.Resources = new List<IDisposable>();
		}

		public bool IsDisposed { get; private set; }
		public bool HasExited { get; private set; }

		public IApplicationLifetime ApplicationLifetime
		{
			get { return this.applicationLifetime; }
			private set { this.applicationLifetime = (ApplicationLifetime)value; }
		} // this will replace the cancellation token on the full node

		public IFullNodeServiceProvider Services { get; set; }

		public NodeSettings Settings { get; private set; }

		public Version Version
		{
			get
			{
				string versionString =
					typeof(FullNode).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
					Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;
				if (!string.IsNullOrEmpty(versionString))
				{
					try
					{
						return new Version(versionString);
					}
					catch (ArgumentException)
					{
					}
					catch (OverflowException)
					{
					}
				}
				return new Version(0, 0);
			}
		}

		public Network Network { get; internal set; }

		public CoinView CoinView { get; set; }

		public DataFolder DataFolder { get; set; }

		public IDateTimeProvider DateTimeProvider { get; set; }

		public bool IsInitialBlockDownload()
		{
			//if (fImporting || fReindex)
			//	return true;
			if (this.ConsensusLoop.Tip == null)
				return true;
			if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
				return true;
			if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() <
			    (this.DateTimeProvider.GetTime() - this.Settings.MaxTipAge))
				return true;
			return false;
		}

		public List<IDisposable> Resources { get; }

		public ChainBehavior.ChainState ChainBehaviorState { get; private set; }

		public Signals Signals { get; set; }

		public ConsensusLoop ConsensusLoop { get; set; }

		public IWebHost RPCHost { get; set; }

		public ConnectionManager ConnectionManager { get; set; }

		public MempoolManager MempoolManager { get; set; }

		public BlockStoreManager BlockStoreManager { get; set; }

		public ConcurrentChain Chain { get; set; }

		public CancellationProvider GlobalCancellation { get; private set; }

		public class CancellationProvider
		{
			public CancellationTokenSource Cancellation { get; set; }
		}

		public FullNode Initialize(IFullNodeServiceProvider serviceProvider)
		{
			Guard.NotNull(serviceProvider, nameof(serviceProvider));

			this.Services = serviceProvider;

			this.DataFolder = this.Services.ServiceProvider.GetService<DataFolder>();
			this.DateTimeProvider = this.Services.ServiceProvider.GetService<IDateTimeProvider>();
			this.Network = this.Services.ServiceProvider.GetService<Network>();
			this.Settings = this.Services.ServiceProvider.GetService<NodeSettings>();
			this.ChainBehaviorState = this.Services.ServiceProvider.GetService<ChainBehavior.ChainState>();
			this.CoinView = this.Services.ServiceProvider.GetService<CoinView>();
			this.Chain = this.Services.ServiceProvider.GetService<ConcurrentChain>();
			this.GlobalCancellation = this.Services.ServiceProvider.GetService<CancellationProvider>();
			this.MempoolManager = this.Services.ServiceProvider.GetService<MempoolManager>();
			this.Signals = this.Services.ServiceProvider.GetService<Signals>();

			this.ConnectionManager = this.Services.ServiceProvider.GetService<ConnectionManager>();
			this.BlockStoreManager = this.Services.ServiceProvider.GetService<BlockStoreManager>();
			this.ConsensusLoop = this.Services.ServiceProvider.GetService<ConsensusLoop>();

			this.logger.LogDebug("Full node initialized on {0}", this.Network.Name);

			return this;
		}

		protected void StartFeatures()
		{
			this.ApplicationLifetime =
				this.Services?.ServiceProvider.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
			this.fullNodeFeatureExecutor = this.Services?.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

			// Fire IApplicationLifetime.Started
			this.applicationLifetime?.NotifyStarted();

			//start all registered features
			this.fullNodeFeatureExecutor?.Start();
		}

		protected internal void DisposeFeatures()
		{
			// Fire IApplicationLifetime.Stopping
			this.ApplicationLifetime?.StopApplication();
			// Fire the IHostedService.Stop
			this.fullNodeFeatureExecutor?.Stop();
			(this.Services.ServiceProvider as IDisposable)?.Dispose();
			//(this.Services.ServiceProvider as IDisposable)?.Dispose();
		}

		public void Start()
		{
			if (this.IsDisposed)
				throw new ObjectDisposedException("FullNode");

			this.isStarted.Reset();

			// start all the features defined
			this.StartFeatures();

			this.ConnectionManager.Start();
			this.isStarted.Set();

			this.StartPeriodicLog();
		}

		public void Stop()
		{
			if (this.Stopped)
				return;

			this.Stopped = true;

			// Fire IApplicationLifetime.Stopping
			this.ApplicationLifetime?.StopApplication();

			if (this.GlobalCancellation != null)
			{
				this.GlobalCancellation.Cancellation.Cancel();

				this.ConnectionManager.Dispose();
				foreach (IDisposable dispo in this.Resources)
					dispo.Dispose();

				this.DisposeFeatures();
			}

			// Fire IApplicationLifetime.Stopped
			this.applicationLifetime?.NotifyStopped();
		}

		private void StartPeriodicLog()
		{
			AsyncLoop.Run("PeriodicLog", (cancellation) =>
				{
					// TODO: move stats to each of its components
					StringBuilder benchLogs = new StringBuilder();

					benchLogs.AppendLine("======Node stats====== " + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture) + " agent " +
					                     this.ConnectionManager.Parameters.UserAgent);
					benchLogs.AppendLine("Headers.Height: ".PadRight(Logs.ColumnLength + 3) +
					                     this.Chain.Tip.Height.ToString().PadRight(8) +
					                     " Headers.Hash: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.HashBlock);

					if (this.ConsensusLoop != null)
					{
						benchLogs.AppendLine("Consensus.Height: ".PadRight(Logs.ColumnLength + 3) +
						                     this.ChainBehaviorState.HighestValidatedPoW.Height.ToString().PadRight(8) +
						                     " Consensus.Hash: ".PadRight(Logs.ColumnLength + 3) +
						                     this.ChainBehaviorState.HighestValidatedPoW.HashBlock);
					}

					if (this.ChainBehaviorState.HighestPersistedBlock != null)
					{
						benchLogs.AppendLine("Store.Height: ".PadRight(Logs.ColumnLength + 3) +
						                     this.ChainBehaviorState.HighestPersistedBlock.Height.ToString().PadRight(8) +
						                     " Store.Hash: ".PadRight(Logs.ColumnLength + 3) +
						                     this.ChainBehaviorState.HighestPersistedBlock.HashBlock);
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
			this.isDisposed.WaitOne();
			this.Dispose();
		}

#pragma warning disable 649
		private Exception uncatchedException;
#pragma warning restore 649

		public void Dispose()
		{
			if (this.IsDisposed)
				return;
			this.IsDisposed = true;

			Logs.FullNode.LogInformation("Closing node pending...");

			if (!this.Stopped)
			{
				try
				{
					this.Stop();
				}
				catch (Exception ex)
				{
					this.logger?.LogError(ex.Message);
				}
			}

			this.isStarted.WaitOne();
			this.isDisposed.Set();
			this.HasExited = true;
		}

		public void ThrowIfUncatchedException()
		{
			if (this.uncatchedException != null)
			{
				var ex = this.uncatchedException;
				var aex = this.uncatchedException as AggregateException;
				if (aex != null)
					ex = aex.InnerException;
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
		}
	}
}
