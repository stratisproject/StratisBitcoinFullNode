using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests")]

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Base node services, these are the services a node has to have.
    /// The ConnectionManager feature is also part of the base but may go in a feature of its own.
    /// The base features are the minimal components required to connect to peers and maintain the best chain.
    /// <para>
    /// The base node services for a node are:
    /// <list type="bullet">
    /// <item>the ConcurrentChain to keep track of the best chain,</item>
    /// <item>the ConnectionManager to connect with the network,</item>
    /// <item>DatetimeProvider and Cancellation,</item>
    /// <item>CancellationProvider and Cancellation,</item>
    /// <item>DataFolder,</item>
    /// <item>ChainState.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class BaseFeature : FullNodeFeature, INodeStats
    {
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Disposable resources that will be disposed when the feature stops.</summary>
        private readonly List<IDisposable> disposableResources;

        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Access to the database of blocks.</summary>
        private readonly ChainRepository chainRepository;

        /// <summary>User defined node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Locations of important folders and files on disk.</summary>
        private readonly DataFolder dataFolder;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Manager of node's network connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Logger for the node.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>State of time synchronization feature that stores collected data samples.</summary>
        private readonly ITimeSyncBehaviorState timeSyncBehaviorState;

        /// <summary>Provider of binary (de)serialization for data stored in the database.</summary>
        private readonly DBreezeSerializer dbreezeSerializer;

        /// <summary>Manager of node's network peers.</summary>
        private IPeerAddressManager peerAddressManager;

        /// <summary>Periodic task to save list of peers to disk.</summary>
        private IAsyncLoop flushAddressManagerLoop;

        /// <summary>Periodic task to save the chain to the database.</summary>
        private IAsyncLoop flushChainLoop;

        /// <summary>A handler that can manage the lifetime of network peers.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Selects the best available chain based on tips provided by the peers and switches to it.</summary>
        private readonly BestChainSelector bestChainSelector;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="dataFolder">Locations of important folders and files on disk.</param>
        /// <param name="nodeLifetime">Global application life cycle control - triggers when application shuts down.</param>
        /// <param name="chain">Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="connectionManager">Manager of node's network connections.</param>
        /// <param name="chainRepository">Access to the database of blocks.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="asyncLoopFactory">Factory for creating background async loop tasks.</param>
        /// <param name="timeSyncBehaviorState">State of time synchronization feature that stores collected data samples.</param>
        /// <param name="dbreezeSerializer">Provider of binary (de)serialization for data stored in the database.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        /// <param name="bestChainSelector">Selects the best available chain based on tips provided by the peers and switches to it.</param>
        public BaseFeature(
            NodeSettings nodeSettings,
            DataFolder dataFolder,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain,
            IChainState chainState,
            IConnectionManager connectionManager,
            ChainRepository chainRepository,
            IDateTimeProvider dateTimeProvider,
            IAsyncLoopFactory asyncLoopFactory,
            ITimeSyncBehaviorState timeSyncBehaviorState,
            DBreezeSerializer dbreezeSerializer,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            IPeerAddressManager peerAddressManager,
            BestChainSelector bestChainSelector)
        {
            this.chainState = Guard.NotNull(chainState, nameof(chainState));
            this.chainRepository = Guard.NotNull(chainRepository, nameof(chainRepository));
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.chain = Guard.NotNull(chain, nameof(chain));
            this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
            this.bestChainSelector = bestChainSelector;
            this.peerBanning = Guard.NotNull(peerBanning, nameof(peerBanning));

            this.peerAddressManager = Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            this.peerAddressManager.PeerFilePath = this.dataFolder;

            this.initialBlockDownloadState = initialBlockDownloadState;
            this.dateTimeProvider = dateTimeProvider;
            this.asyncLoopFactory = asyncLoopFactory;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.loggerFactory = loggerFactory;
            this.dbreezeSerializer = dbreezeSerializer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.disposableResources = new List<IDisposable>();
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            benchLogs.AppendLine("Headers.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                    this.chain.Tip.Height.ToString().PadRight(8) +
                                    " Headers.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + this.chain.Tip.HashBlock);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.logger.LogTrace("()");

            this.dbreezeSerializer.Initialize(this.chain.Network);

            this.StartChainAsync().GetAwaiter().GetResult();

            var connectionParameters = this.connectionManager.Parameters;
            connectionParameters.IsRelay = !this.nodeSettings.ConfigReader.GetOrDefault("blocksonly", false);
            connectionParameters.TemplateBehaviors.Add(new ChainHeadersBehavior(this.chain, this.chainState, this.initialBlockDownloadState, this.bestChainSelector, this.loggerFactory));
            connectionParameters.TemplateBehaviors.Add(new PeerBanningBehavior(this.loggerFactory, this.peerBanning, this.nodeSettings));

            this.StartAddressManager(connectionParameters);

            if (this.nodeSettings.SyncTimeEnabled)
            {
                connectionParameters.TemplateBehaviors.Add(new TimeSyncBehavior(this.timeSyncBehaviorState, this.dateTimeProvider, this.loggerFactory));
            }
            else
            {
                this.logger.LogDebug("Time synchronization with peers is disabled.");
            }

            this.disposableResources.Add(this.timeSyncBehaviorState as IDisposable);
            this.disposableResources.Add(this.chainRepository);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            NodeSettings.PrintHelp(network);
        }
        
        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            NodeSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <summary>
        /// Initializes node's chain repository.
        /// Creates periodic task to persist changes to the database.
        /// </summary>
        private async Task StartChainAsync()
        {
            if (!Directory.Exists(this.dataFolder.ChainPath))
            {
                this.logger.LogInformation("Creating " + this.dataFolder.ChainPath);
                Directory.CreateDirectory(this.dataFolder.ChainPath);
            }

            this.logger.LogInformation("Loading chain");
            await this.chainRepository.LoadAsync(this.chain).ConfigureAwait(false);

            this.logger.LogInformation("Chain loaded at height " + this.chain.Height);

            this.flushChainLoop = this.asyncLoopFactory.Run("FlushChain", async token =>
            {
                await this.chainRepository.SaveAsync(this.chain);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(5.0),
            startAfter: TimeSpan.FromMinutes(5.0));
        }

        /// <summary>
        /// Initializes node's address manager. Loads previously known peers from the file
        /// or creates new peer file if it does not exist. Creates periodic task to persist changes
        /// in peers to disk.
        /// </summary>
        private void StartAddressManager(NetworkPeerConnectionParameters connectionParameters)
        {
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager);
            connectionParameters.TemplateBehaviors.Add(addressManagerBehaviour);

            if (File.Exists(Path.Combine(this.dataFolder.AddressManagerFilePath, PeerAddressManager.PeerFileName)))
            {
                this.logger.LogInformation($"Loading peers from : {this.dataFolder.AddressManagerFilePath}...");
                this.peerAddressManager.LoadPeers();
            }

            this.flushAddressManagerLoop = this.asyncLoopFactory.Run("Periodic peer flush...", token =>
            {
                this.peerAddressManager.SavePeers();
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(5.0),
            startAfter: TimeSpan.FromMinutes(5.0));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.logger.LogInformation("Flushing peers...");
            this.flushAddressManagerLoop.Dispose();

            this.peerAddressManager.Dispose();

            this.logger.LogInformation("Flushing headers chain...");
            this.flushChainLoop?.Dispose();

            this.chainRepository.SaveAsync(this.chain).GetAwaiter().GetResult();

            foreach (IDisposable disposable in this.disposableResources)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderBaseFeatureExtension
    {
        /// <summary>
        /// Makes the full node use all the required features - <see cref="BaseFeature"/>.
        /// </summary>
        /// <param name="fullNodeBuilder">Builder responsible for creating the node.</param>
        /// <returns>Full node builder's interface to allow fluent code.</returns>
        public static IFullNodeBuilder UseBaseFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BaseFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<DBreezeSerializer>();
                    services.AddSingleton(fullNodeBuilder.NodeSettings.LoggerFactory);
                    services.AddSingleton(fullNodeBuilder.NodeSettings.DataFolder);
                    services.AddSingleton<INodeLifetime, NodeLifetime>();
                    services.AddSingleton<IPeerBanning, PeerBanning>();
                    services.AddSingleton<FullNodeFeatureExecutor>();
                    services.AddSingleton<Signals.Signals>().AddSingleton<ISignals, Signals.Signals>(provider => provider.GetService<Signals.Signals>());
                    services.AddSingleton<FullNode>().AddSingleton((provider) => { return provider.GetService<FullNode>() as IFullNode; });
                    services.AddSingleton<ConcurrentChain>(new ConcurrentChain(fullNodeBuilder.Network));
                    services.AddSingleton<IDateTimeProvider>(DateTimeProvider.Default);
                    services.AddSingleton<IInvalidBlockHashStore, InvalidBlockHashStore>();
                    services.AddSingleton<IChainState, ChainState>();
                    services.AddSingleton<ChainRepository>();
                    services.AddSingleton<ITimeSyncBehaviorState, TimeSyncBehaviorState>();
                    services.AddSingleton<IAsyncLoopFactory, AsyncLoopFactory>();
                    services.AddSingleton<NodeDeployments>();

                    // Connection
                    services.AddSingleton<INetworkPeerFactory, NetworkPeerFactory>();
                    services.AddSingleton<NetworkPeerConnectionParameters>(new NetworkPeerConnectionParameters());
                    services.AddSingleton<IConnectionManager, ConnectionManager>();
                    services.AddSingleton<ConnectionManagerSettings>();
                    services.AddSingleton<PayloadProvider>(new PayloadProvider().DiscoverPayloads());
                    services.AddSingleton<BestChainSelector>();

                    // Peer address manager
                    services.AddSingleton<IPeerAddressManager, PeerAddressManager>();
                    services.AddSingleton<IPeerConnector, PeerConnectorAddNode>();
                    services.AddSingleton<IPeerConnector, PeerConnectorConnectNode>();
                    services.AddSingleton<IPeerConnector, PeerConnectorDiscovery>();
                    services.AddSingleton<IPeerDiscovery, PeerDiscovery>();
                    services.AddSingleton<ISelfEndpointTracker, SelfEndpointTracker>();
                });
            });

            return fullNodeBuilder;
        }
    }
}