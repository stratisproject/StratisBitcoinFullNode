using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
using Stratis.Bitcoin.Utilities;
using System.Reflection;
using Stratis.Bitcoin.Wallet;

namespace Stratis.Bitcoin
{

	public class FullNode : IFullNode
	{
		private readonly ILogger logger;
		private ApplicationLifetime applicationLifetime;
		private FullNodeFeatureExecutor fullNodeFeatureExecutor;

		internal bool Stopped;

		public FullNode()
		{
			this.logger = Logs.LoggerFactory.CreateLogger<FullNode>();
		}

		public bool IsDisposed { get; private set; }
		public bool HasExited { get; private set; }

		public IApplicationLifetime ApplicationLifetime
		{
			get { return this.applicationLifetime; }
			private set { this.applicationLifetime = (ApplicationLifetime)value; }
		} 

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
            // if consensus is no present IBD has no meaning
		    if (this.ConsensusLoop == null)
		        return false;

            if (this.ConsensusLoop.Tip == null)
				return true;
			if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
				return true;
			if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() <
			    (this.DateTimeProvider.GetTime() - this.Settings.MaxTipAge))
				return true;
			return false;
		}

		public List<IDisposable> Resources { get; private set; }

		public ChainBehavior.ChainState ChainBehaviorState { get; private set; }

		public Signals Signals { get; set; }

		public ConsensusLoop ConsensusLoop { get; set; }

		public WalletManager WalletManager { get; set; }

		public IWebHost RPCHost { get; set; }

		public IConnectionManager ConnectionManager { get; set; }

		public MempoolManager MempoolManager { get; set; }

		public BlockStoreManager BlockStoreManager { get; set; }

		public ConcurrentChain Chain { get; set; }

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
			this.MempoolManager = this.Services.ServiceProvider.GetService<MempoolManager>();
			this.Signals = this.Services.ServiceProvider.GetService<Signals>();

			this.ConnectionManager = this.Services.ServiceProvider.GetService<IConnectionManager>();
			this.BlockStoreManager = this.Services.ServiceProvider.GetService<BlockStoreManager>();
			this.ConsensusLoop = this.Services.ServiceProvider.GetService<ConsensusLoop>();
			this.WalletManager = this.Services.ServiceProvider.GetService<IWalletManager>() as WalletManager;

			Logs.FullNode.LogInformation($"Full node initialized on {this.Network.Name}");

			return this;
		}

		public void Start()
		{
			if (this.IsDisposed)
				throw new ObjectDisposedException(nameof(FullNode));

		    if (this.Resources != null)
		        throw new InvalidOperationException("node has already started.");

		    this.Resources = new List<IDisposable>();
            this.ApplicationLifetime = this.Services.ServiceProvider.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
		    this.fullNodeFeatureExecutor = this.Services.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

		    if (this.ApplicationLifetime == null)
		        throw new InvalidOperationException($"{nameof(IApplicationLifetime)} must be set.");

            if (this.fullNodeFeatureExecutor == null)
		        throw new InvalidOperationException($"{nameof(FullNodeFeatureExecutor)} must be set.");

		    Logs.FullNode.LogInformation("Starting node...");

            // start all registered features
            this.fullNodeFeatureExecutor.Start();

            // start connecting to peers
			this.ConnectionManager.Start();

		    // Fire IApplicationLifetime.Started
		    this.applicationLifetime.NotifyStarted();

            this.StartPeriodicLog();
		}

		public void Stop()
		{
			if (this.Stopped)
				return;

			this.Stopped = true;

		    Logs.FullNode.LogInformation("Closing node pending...");

            // Fire IApplicationLifetime.Stopping
            this.ApplicationLifetime.StopApplication();

		    this.ConnectionManager.Dispose();

            foreach (IDisposable dispo in this.Resources)
		        dispo.Dispose();

            // Fire the NodeFeatureExecutor.Stop
            this.fullNodeFeatureExecutor.Stop();
            (this.Services.ServiceProvider as IDisposable)?.Dispose();

            // Fire IApplicationLifetime.Stopped
            this.applicationLifetime.NotifyStopped();
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

					if (this.WalletManager != null)
					{
						var height = this.WalletManager.LastBlockHeight();
					    var block = this.Chain.GetBlock(height);
					    var hashBlock = block == null ? uint256.Zero : block.HashBlock;

                        benchLogs.AppendLine("Wallet.Height: ".PadRight(Logs.ColumnLength + 3) +
						                     height.ToString().PadRight(8) +
											 " Wallet.Hash: ".PadRight(Logs.ColumnLength + 3) +
                                             hashBlock);
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
				this.applicationLifetime.ApplicationStopping,
				repeatEvery: TimeSpans.FiveSeconds,
				startAfter: TimeSpans.FiveSeconds);
		}

		public void Dispose()
		{
		    if (this.IsDisposed)
		        return;

            this.IsDisposed = true;

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

			this.HasExited = true;
		}
	}
}
