using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
    /// <summary>
    /// Node providing all supported features of the blockchain and its network.
    /// </summary>
    public class FullNode : IFullNode
    {
        /// <summary>Instance logger.</summary>
        private ILogger logger;

        /// <summary>Component responsible for starting and stopping all the node's features.</summary>
        private FullNodeFeatureExecutor fullNodeFeatureExecutor;

        /// <summary>Indicates whether the node has been stopped or is currently being stopped.</summary>
        internal bool Stopped;

        /// <summary>Indicates whether the node's instance has been disposed or is currently being disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Indicates whether the node's instance disposal has been finished.</summary>
        public bool HasExited { get; private set; }

        /// <summary>Node command line and configuration file settings.</summary>
        public NodeSettings Settings { get; private set; }

        /// <summary>List of disposable resources that the node uses.</summary>
        public List<IDisposable> Resources { get; private set; }

        /// <summary>Information about the best chain.</summary>
        public ChainState ChainBehaviorState { get; private set; }

        /// <summary>Provider of notification about newly available blocks and transactions.</summary>
        public Signals.Signals Signals { get; set; }

        /// <summary>Component responsible for keeping the node in consensus with the network.</summary>
        public ConsensusLoop ConsensusLoop { get; set; }

        /// <summary>Manager providing operations on wallets.</summary>
        public WalletManager WalletManager { get; set; }

        /// <summary>A transaction handler providing operations on transactions in the wallets.</summary>
        public IWalletTransactionHandler WalletTransactionHandler { get; set; }

        /// <summary>ASP.NET Core host for RPC server.</summary>
        public IWebHost RPCHost { get; set; }

        /// <summary>Component responsible for connections to peers in P2P network.</summary>
        public IConnectionManager ConnectionManager { get; set; }

        /// <summary>Manager of transactions in memory pool.</summary>
        public MempoolManager MempoolManager { get; set; }

        /// <summary>Manager responsible for persistence of blocks.</summary>
        public BlockStoreManager BlockStoreManager { get; set; }

        /// <summary>Best chain of block headers from genesis.</summary>
        public ConcurrentChain Chain { get; set; }

        /// <summary>Factory for creating and execution of asynchronous loops.</summary>
        public IAsyncLoopFactory AsyncLoopFactory { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; internal set; }

        /// <summary>Information about transaction outputs in the chain.</summary>
        public CoinView CoinView { get; set; }

        /// <summary>Contains path locations to folders and files on disk.</summary>
        public DataFolder DataFolder { get; set; }

        /// <summary>Provider of date time functionality.</summary>
        public IDateTimeProvider DateTimeProvider { get; set; }

        /// <summary>Application life cycle control - triggers when application shuts down.</summary>
        private NodeLifetime nodeLifetime;

        /// <inheritdoc />
        public INodeLifetime NodeLifetime
        {
            get { return this.nodeLifetime; }
            private set { this.nodeLifetime = (NodeLifetime)value; }
        }

        /// <inheritdoc />
        public IFullNodeServiceProvider Services { get; set; }

        /// <inheritdoc />
        public Version Version
        {
            get
            {
                string versionString = typeof(FullNode).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
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

        /// <summary>
        /// Checks whether the node is currently in the process of initial block download.
        /// </summary>
        /// <returns><c>true</c> if the node is currently doing IBD, <c>false</c> otherwise.</returns>
        public bool IsInitialBlockDownload()
        {
            // if consensus is no present IBD has no meaning
            if (this.ConsensusLoop == null)
                return false;

            if (this.ConsensusLoop.Tip == null)
                return true;

            if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
                return true;

            if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.Settings.MaxTipAge))
                return true;

            return false;
        }

        /// <summary>
        /// Initializes DI services that the node needs.
        /// </summary>
        /// <param name="serviceProvider">Provider of DI services.</param>
        /// <returns>Full node itself to allow fluent code.</returns>
        public FullNode Initialize(IFullNodeServiceProvider serviceProvider)
        {
            Guard.NotNull(serviceProvider, nameof(serviceProvider));

            this.Services = serviceProvider;

            this.logger = this.Services.ServiceProvider.GetService<ILoggerFactory>().CreateLogger(this.GetType().FullName);

            this.DataFolder = this.Services.ServiceProvider.GetService<DataFolder>();
            this.DateTimeProvider = this.Services.ServiceProvider.GetService<IDateTimeProvider>();
            this.Network = this.Services.ServiceProvider.GetService<Network>();
            this.Settings = this.Services.ServiceProvider.GetService<NodeSettings>();
            this.ChainBehaviorState = this.Services.ServiceProvider.GetService<ChainState>();
            this.CoinView = this.Services.ServiceProvider.GetService<CoinView>();
            this.Chain = this.Services.ServiceProvider.GetService<ConcurrentChain>();
            this.MempoolManager = this.Services.ServiceProvider.GetService<MempoolManager>();
            this.Signals = this.Services.ServiceProvider.GetService<Signals.Signals>();

            this.ConnectionManager = this.Services.ServiceProvider.GetService<IConnectionManager>();
            this.BlockStoreManager = this.Services.ServiceProvider.GetService<BlockStoreManager>();
            this.ConsensusLoop = this.Services.ServiceProvider.GetService<ConsensusLoop>();
            this.WalletManager = this.Services.ServiceProvider.GetService<IWalletManager>() as WalletManager;
            this.WalletTransactionHandler = this.Services.ServiceProvider.GetService<IWalletTransactionHandler>();
            this.AsyncLoopFactory = this.Services.ServiceProvider.GetService<IAsyncLoopFactory>();

            this.logger.LogInformation($"Full node initialized on {this.Network.Name}");

            return this;
        }

        /// <inheritdoc />
        public void Start()
        {
            if (this.IsDisposed)
                throw new ObjectDisposedException(nameof(FullNode));

            if (this.Resources != null)
                throw new InvalidOperationException("node has already started.");

            this.Resources = new List<IDisposable>();
            this.nodeLifetime = this.Services.ServiceProvider.GetRequiredService<INodeLifetime>() as NodeLifetime;
            this.fullNodeFeatureExecutor = this.Services.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

            if (this.nodeLifetime == null)
                throw new InvalidOperationException($"{nameof(INodeLifetime)} must be set.");

            if (this.fullNodeFeatureExecutor == null)
                throw new InvalidOperationException($"{nameof(FullNodeFeatureExecutor)} must be set.");

            this.logger.LogInformation("Starting node...");

            // start all registered features
            this.fullNodeFeatureExecutor.Start();

            // start connecting to peers
            this.ConnectionManager.Start();

            // Fire INodeLifetime.Started
            this.nodeLifetime.NotifyStarted();

            this.StartPeriodicLog();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.Stopped)
                return;

            this.Stopped = true;

            this.logger.LogInformation("Closing node pending...");

            // Fire INodeLifetime.Stopping.
            this.nodeLifetime.StopApplication();

            this.ConnectionManager.Dispose();

            foreach (IDisposable dispo in this.Resources)
                dispo.Dispose();

            // Fire the NodeFeatureExecutor.Stop.
            this.fullNodeFeatureExecutor.Stop();
            (this.Services.ServiceProvider as IDisposable)?.Dispose();

            // Fire INodeLifetime.Stopped.
            this.nodeLifetime.NotifyStopped();
        }

        /// <summary>
        /// Starts a loop to periodically log statistics about node's status very couple of seconds.
        /// <para>
        /// These logs are also displayed on the console.
        /// </para>
        /// </summary>
        private void StartPeriodicLog()
        {
            this.AsyncLoopFactory.Run("PeriodicLog", (cancellation) =>
            {
                // TODO: move stats to each of its components
                StringBuilder benchLogs = new StringBuilder();

                benchLogs.AppendLine("======Node stats====== " + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture) + " agent " +
                                     this.ConnectionManager.Parameters.UserAgent);
                benchLogs.AppendLine("Headers.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                     this.Chain.Tip.Height.ToString().PadRight(8) +
                                     " Headers.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) + this.Chain.Tip.HashBlock);

                if (this.ConsensusLoop != null)
                {
                    benchLogs.AppendLine("Consensus.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestValidatedPoW.Height.ToString().PadRight(8) +
                                         " Consensus.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestValidatedPoW.HashBlock);
                }

                if (this.ChainBehaviorState.HighestPersistedBlock != null)
                {
                    benchLogs.AppendLine("Store.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestPersistedBlock.Height.ToString().PadRight(8) +
                                         " Store.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestPersistedBlock.HashBlock);
                }

                if (this.ChainBehaviorState.HighestIndexedBlock != null)
                {
                    benchLogs.AppendLine("Index.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestIndexedBlock.Height.ToString().PadRight(8) +
                                         " Index.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         this.ChainBehaviorState.HighestIndexedBlock.HashBlock);
                }

                if (this.WalletManager != null)
                {
                    var height = this.WalletManager.LastBlockHeight();
                    var block = this.Chain.GetBlock(height);
                    var hashBlock = block == null ? uint256.Zero : block.HashBlock;

                    benchLogs.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                         height.ToString().PadRight(8) +
                                         " Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
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
                this.logger.LogInformation(benchLogs.ToString());
                return Task.CompletedTask;
            },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.FiveSeconds,
                startAfter: TimeSpans.FiveSeconds);
        }

        /// <inheritdoc />
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
