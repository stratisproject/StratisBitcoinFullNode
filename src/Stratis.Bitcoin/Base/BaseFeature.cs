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
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Tests")]
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
        private readonly IChainRepository chainRepository;

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

        /// <inheritdoc cref="Network"/>
        private readonly Network network;

        private readonly IConsensusManager consensusManager;
        private readonly IConsensusRuleEngine consensusRules;
        private readonly IPartialValidator partialValidator;
        private readonly IBlockPuller blockPuller;
        private readonly IBlockStore blockStore;

        /// <inheritdoc cref="IFinalizedBlockInfo"/>
        private readonly IFinalizedBlockInfo finalizedBlockInfo;

        public BaseFeature(
            NodeSettings nodeSettings,
            DataFolder dataFolder,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain,
            IChainState chainState,
            IConnectionManager connectionManager,
            IChainRepository chainRepository,
            IFinalizedBlockInfo finalizedBlockInfo,
            IDateTimeProvider dateTimeProvider,
            IAsyncLoopFactory asyncLoopFactory,
            ITimeSyncBehaviorState timeSyncBehaviorState,
            DBreezeSerializer dbreezeSerializer,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            IPeerAddressManager peerAddressManager,
            IConsensusManager consensusManager,
            IConsensusRuleEngine consensusRules,
            IPartialValidator partialValidator,
            IBlockPuller blockPuller,
            IBlockStore blockStore,
            Network network)
        {
            this.chainState = Guard.NotNull(chainState, nameof(chainState));
            this.chainRepository = Guard.NotNull(chainRepository, nameof(chainRepository));
            this.finalizedBlockInfo = Guard.NotNull(finalizedBlockInfo, nameof(finalizedBlockInfo));
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.chain = Guard.NotNull(chain, nameof(chain));
            this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
            this.consensusManager = consensusManager;
            this.consensusRules = consensusRules;
            this.partialValidator = partialValidator;
            this.blockPuller = blockPuller;
            this.blockStore = blockStore;
            this.network = network;
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
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.logger.LogTrace("()");

            this.dbreezeSerializer.Initialize(this.chain.Network);

            this.StartChainAsync().GetAwaiter().GetResult();

            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;
            connectionParameters.IsRelay = this.connectionManager.ConnectionSettings.RelayTxes;

            connectionParameters.TemplateBehaviors.Add(new PingPongBehavior());
            connectionParameters.TemplateBehaviors.Add(new ConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.connectionManager, this.loggerFactory));
            connectionParameters.TemplateBehaviors.Add(new PeerBanningBehavior(this.loggerFactory, this.peerBanning, this.nodeSettings));
            connectionParameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.initialBlockDownloadState, this.loggerFactory));

            this.StartAddressManager(connectionParameters);

            if (this.connectionManager.ConnectionSettings.SyncTimeEnabled)
            {
                connectionParameters.TemplateBehaviors.Add(new TimeSyncBehavior(this.timeSyncBehaviorState, this.dateTimeProvider, this.loggerFactory));
            }
            else
            {
                this.logger.LogDebug("Time synchronization with peers is disabled.");
            }

            this.disposableResources.Add(this.timeSyncBehaviorState as IDisposable);
            this.disposableResources.Add(this.chainRepository);

            // Block store must be initialized before consensus manager.
            // This may be a temporary solution until a better way is found to solve this dependency.
            this.blockStore.InitializeAsync().GetAwaiter().GetResult();

            this.consensusRules.Initialize().GetAwaiter().GetResult();

            this.consensusManager.InitializeAsync(this.chain.Tip).GetAwaiter().GetResult();

            this.consensusRules.Register();

            this.chainState.ConsensusTip = this.consensusManager.Tip;

            this.logger.LogTrace("(-)");
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

            this.logger.LogInformation("Loading finalized block height");
            await this.finalizedBlockInfo.LoadFinalizedBlockInfoAsync(this.network).ConfigureAwait(false);

            this.logger.LogInformation("Loading chain");
            ChainedHeader chainTip = await this.chainRepository.LoadAsync(this.chain.Genesis).ConfigureAwait(false);
            this.chain.SetTip(chainTip);

            this.logger.LogInformation("Chain loaded at height " + this.chain.Height);

            this.flushChainLoop = this.asyncLoopFactory.Run("FlushChain", async token =>
            {
                await this.chainRepository.SaveAsync(this.chain);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(1.0),
            startAfter: TimeSpan.FromMinutes(1.0));
        }

        /// <summary>
        /// Initializes node's address manager. Loads previously known peers from the file
        /// or creates new peer file if it does not exist. Creates periodic task to persist changes
        /// in peers to disk.
        /// </summary>
        private void StartAddressManager(NetworkPeerConnectionParameters connectionParameters)
        {
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager, this.loggerFactory);
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

            this.partialValidator.Dispose();

            this.logger.LogInformation("Flushing headers chain...");
            this.flushChainLoop?.Dispose();

            this.chainRepository.SaveAsync(this.chain).GetAwaiter().GetResult();

            foreach (IDisposable disposable in this.disposableResources)
            {
                disposable.Dispose();
            }

            this.blockPuller.Dispose();

            this.consensusManager.Dispose();
            this.consensusRules.Dispose();

            this.blockStore.Dispose();
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
                    services.AddSingleton<IChainRepository, ChainRepository>().AddSingleton<IFinalizedBlockInfo, ChainRepository>(provider => provider.GetService<IChainRepository>() as ChainRepository);
                    services.AddSingleton<ITimeSyncBehaviorState, TimeSyncBehaviorState>();
                    services.AddSingleton<IAsyncLoopFactory, AsyncLoopFactory>();
                    services.AddSingleton<NodeDeployments>();
                    services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();

                    // Consensus
                    services.AddSingleton<ConsensusSettings>();
                    services.AddSingleton<ICheckpoints, Checkpoints>();

                    // Connection
                    services.AddSingleton<INetworkPeerFactory, NetworkPeerFactory>();
                    services.AddSingleton<NetworkPeerConnectionParameters>();
                    services.AddSingleton<IConnectionManager, ConnectionManager>();
                    services.AddSingleton<ConnectionManagerSettings>();
                    services.AddSingleton<PayloadProvider>(new PayloadProvider().DiscoverPayloads());
                    services.AddSingleton<IVersionProvider, VersionProvider>();
                    services.AddSingleton<IBlockPuller, BlockPuller>();

                    // Peer address manager
                    services.AddSingleton<IPeerAddressManager, PeerAddressManager>();
                    services.AddSingleton<IPeerConnector, PeerConnectorAddNode>();
                    services.AddSingleton<IPeerConnector, PeerConnectorConnectNode>();
                    services.AddSingleton<IPeerConnector, PeerConnectorDiscovery>();
                    services.AddSingleton<IPeerDiscovery, PeerDiscovery>();
                    services.AddSingleton<ISelfEndpointTracker, SelfEndpointTracker>();

                    // Consensus
                    services.AddSingleton<IConsensusManager, ConsensusManager>();
                    services.AddSingleton<IHeaderValidator, HeaderValidator>();
                    services.AddSingleton<IIntegrityValidator, IntegrityValidator>();
                    services.AddSingleton<IPartialValidator, PartialValidator>();
                    services.AddSingleton<IFullValidator, FullValidator>();
                });
            });

            return fullNodeBuilder;
        }
    }
}