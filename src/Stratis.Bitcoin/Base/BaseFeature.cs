using System;
using System.IO;
using System.Runtime.CompilerServices;
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
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Tests.Common")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests.Common")]
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
    public sealed class BaseFeature : FullNodeFeature
    {
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

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

        private readonly IProvenBlockHeaderStore provenBlockHeaderStore;

        private readonly IConsensusManager consensusManager;
        private readonly IConsensusRuleEngine consensusRules;
        private readonly IBlockPuller blockPuller;
        private readonly IBlockStore blockStore;
        private readonly ITipsManager tipsManager;
        private readonly IKeyValueRepository keyValueRepo;

        /// <inheritdoc cref="IFinalizedBlockInfoRepository"/>
        private readonly IFinalizedBlockInfoRepository finalizedBlockInfoRepository;

        /// <inheritdoc cref="IPartialValidator"/>
        private readonly IPartialValidator partialValidator;

        public BaseFeature(NodeSettings nodeSettings,
            DataFolder dataFolder,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain,
            IChainState chainState,
            IConnectionManager connectionManager,
            IChainRepository chainRepository,
            IFinalizedBlockInfoRepository finalizedBlockInfo,
            IDateTimeProvider dateTimeProvider,
            IAsyncLoopFactory asyncLoopFactory,
            ITimeSyncBehaviorState timeSyncBehaviorState,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            IPeerAddressManager peerAddressManager,
            IConsensusManager consensusManager,
            IConsensusRuleEngine consensusRules,
            IPartialValidator partialValidator,
            IBlockPuller blockPuller,
            IBlockStore blockStore,
            Network network,
            ITipsManager tipsManager,
            IKeyValueRepository keyValueRepo,
            IProvenBlockHeaderStore provenBlockHeaderStore = null)
        {
            this.chainState = Guard.NotNull(chainState, nameof(chainState));
            this.chainRepository = Guard.NotNull(chainRepository, nameof(chainRepository));
            this.finalizedBlockInfoRepository = Guard.NotNull(finalizedBlockInfo, nameof(finalizedBlockInfo));
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.dataFolder = Guard.NotNull(dataFolder, nameof(dataFolder));
            this.nodeLifetime = Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            this.chain = Guard.NotNull(chain, nameof(chain));
            this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
            this.consensusManager = consensusManager;
            this.consensusRules = consensusRules;
            this.blockPuller = blockPuller;
            this.blockStore = blockStore;
            this.network = network;
            this.provenBlockHeaderStore = provenBlockHeaderStore;
            this.partialValidator = partialValidator;
            this.peerBanning = Guard.NotNull(peerBanning, nameof(peerBanning));
            this.tipsManager = Guard.NotNull(tipsManager, nameof(tipsManager));
            this.keyValueRepo = Guard.NotNull(keyValueRepo, nameof(keyValueRepo));

            this.peerAddressManager = Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            this.peerAddressManager.PeerFilePath = this.dataFolder;

            this.initialBlockDownloadState = initialBlockDownloadState;
            this.dateTimeProvider = dateTimeProvider;
            this.asyncLoopFactory = asyncLoopFactory;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync()
        {
            // TODO rewrite chain starting logic. Tips manager should be used.

            await this.StartChainAsync().ConfigureAwait(false);

            if (this.provenBlockHeaderStore != null)
            {
                // If we find at this point that proven header store is behind chain we can rewind chain (this will cause a ripple effect and rewind block store and consensus)
                // This problem should go away once we implement a component to keep all tips up to date
                // https://github.com/stratisproject/StratisBitcoinFullNode/issues/2503
                ChainedHeader initializedAt = await this.provenBlockHeaderStore.InitializeAsync(this.chain.Tip);
                this.chain.SetTip(initializedAt);
            }

            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;
            connectionParameters.IsRelay = this.connectionManager.ConnectionSettings.RelayTxes;

            connectionParameters.TemplateBehaviors.Add(new PingPongBehavior());
            connectionParameters.TemplateBehaviors.Add(new ConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory));

            // TODO: Once a proper rate limiting strategy has been implemented, this check will be removed.
            if (!this.network.IsRegTest())
                connectionParameters.TemplateBehaviors.Add(new RateLimitingBehavior(this.dateTimeProvider, this.loggerFactory, this.peerBanning));

            connectionParameters.TemplateBehaviors.Add(new PeerBanningBehavior(this.loggerFactory, this.peerBanning, this.nodeSettings));
            connectionParameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.initialBlockDownloadState, this.dateTimeProvider, this.loggerFactory));
            connectionParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(this.connectionManager, this.loggerFactory));

            this.StartAddressManager(connectionParameters);

            if (this.connectionManager.ConnectionSettings.SyncTimeEnabled)
            {
                connectionParameters.TemplateBehaviors.Add(new TimeSyncBehavior(this.timeSyncBehaviorState, this.dateTimeProvider, this.loggerFactory));
            }
            else
            {
                this.logger.LogDebug("Time synchronization with peers is disabled.");
            }

            // Block store must be initialized before consensus manager.
            // This may be a temporary solution until a better way is found to solve this dependency.
            await this.blockStore.InitializeAsync().ConfigureAwait(false);

            await this.consensusRules.InitializeAsync(this.chain.Tip).ConfigureAwait(false);

            this.consensusRules.Register();

            await this.consensusManager.InitializeAsync(this.chain.Tip).ConfigureAwait(false);

            this.chainState.ConsensusTip = this.consensusManager.Tip;
        }

        /// <summary>
        /// Initializes node's chain repository.
        /// Creates periodic task to persist changes to the database.
        /// </summary>
        private async Task StartChainAsync()
        {
            if (!Directory.Exists(this.dataFolder.ChainPath))
            {
                this.logger.LogInformation("Creating {0}.", this.dataFolder.ChainPath);
                Directory.CreateDirectory(this.dataFolder.ChainPath);
            }

            if (!Directory.Exists(this.dataFolder.KeyValueRepositoryPath))
            {
                this.logger.LogInformation("Creating {0}.", this.dataFolder.KeyValueRepositoryPath);
                Directory.CreateDirectory(this.dataFolder.KeyValueRepositoryPath);
            }

            this.logger.LogInformation("Loading finalized block height.");
            await this.finalizedBlockInfoRepository.LoadFinalizedBlockInfoAsync(this.network).ConfigureAwait(false);

            this.logger.LogInformation("Loading chain.");
            ChainedHeader chainTip = await this.chainRepository.LoadAsync(this.chain.Genesis).ConfigureAwait(false);
            this.chain.SetTip(chainTip);

            this.logger.LogInformation("Chain loaded at height {0}.", this.chain.Height);

            this.flushChainLoop = this.asyncLoopFactory.Run("FlushChain", async token =>
            {
                await this.chainRepository.SaveAsync(this.chain).ConfigureAwait(false);

                if (this.provenBlockHeaderStore != null)
                    await this.provenBlockHeaderStore.SaveAsync().ConfigureAwait(false);
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
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(this.dateTimeProvider, this.peerAddressManager, this.peerBanning, this.loggerFactory);
            connectionParameters.TemplateBehaviors.Add(addressManagerBehaviour);

            if (File.Exists(Path.Combine(this.dataFolder.AddressManagerFilePath, PeerAddressManager.PeerFileName)))
            {
                this.logger.LogInformation($"Loading peers from : {this.dataFolder.AddressManagerFilePath}.");
                this.peerAddressManager.LoadPeers();
            }

            this.flushAddressManagerLoop = this.asyncLoopFactory.Run("Periodic peer flush", token =>
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
            this.logger.LogInformation("Flushing peers.");
            this.flushAddressManagerLoop.Dispose();

            this.logger.LogInformation("Disposing peer address manager.");
            this.peerAddressManager.Dispose();

            if (this.flushChainLoop != null)
            {
                this.logger.LogInformation("Flushing headers chain.");
                this.flushChainLoop.Dispose();
            }

            this.logger.LogInformation("Disposing time sync behavior.");
            this.timeSyncBehaviorState.Dispose();

            this.logger.LogInformation("Disposing block puller.");
            this.blockPuller.Dispose();

            this.logger.LogInformation("Disposing partial validator.");
            this.partialValidator.Dispose();

            this.logger.LogInformation("Disposing consensus manager.");
            this.consensusManager.Dispose();

            this.logger.LogInformation("Disposing consensus rules.");
            this.consensusRules.Dispose();

            this.logger.LogInformation("Saving chain repository.");
            this.chainRepository.SaveAsync(this.chain).GetAwaiter().GetResult();
            this.chainRepository.Dispose();

            if (this.provenBlockHeaderStore != null)
            {
                this.logger.LogInformation("Saving proven header store.");
                this.provenBlockHeaderStore.SaveAsync().GetAwaiter().GetResult();
                this.provenBlockHeaderStore.Dispose();
            }

            this.logger.LogInformation("Disposing finalized block info repository.");
            this.finalizedBlockInfoRepository.Dispose();

            this.logger.LogInformation("Disposing block store.");
            this.blockStore.Dispose();

            this.keyValueRepo.Dispose();
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
                    services.AddSingleton<ISignals, Signals.Signals>();
                    services.AddSingleton<FullNode>().AddSingleton((provider) => { return provider.GetService<FullNode>() as IFullNode; });
                    services.AddSingleton<ConcurrentChain>(new ConcurrentChain(fullNodeBuilder.Network));
                    services.AddSingleton<IDateTimeProvider>(DateTimeProvider.Default);
                    services.AddSingleton<IInvalidBlockHashStore, InvalidBlockHashStore>();
                    services.AddSingleton<IChainState, ChainState>();
                    services.AddSingleton<IChainRepository, ChainRepository>();
                    services.AddSingleton<IFinalizedBlockInfoRepository, FinalizedBlockInfoRepository>();
                    services.AddSingleton<ITimeSyncBehaviorState, TimeSyncBehaviorState>();
                    services.AddSingleton<IAsyncLoopFactory, AsyncLoopFactory>();
                    services.AddSingleton<NodeDeployments>();
                    services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadState>();
                    services.AddSingleton<IKeyValueRepository, KeyValueRepository>();
                    services.AddSingleton<ITipsManager, TipsManager>();

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
                    // Consensus manager is created like that due to CM's constructor being internal. This is done
                    // in order to prevent access to CM creation and CHT usage from another features. CHT is supposed
                    // to be used only by CM and no other component.
                    services.AddSingleton<IConsensusManager>(provider => new ConsensusManager(
                        chainedHeaderTree: provider.GetService<IChainedHeaderTree>(),
                        network: provider.GetService<Network>(),
                        loggerFactory: provider.GetService<ILoggerFactory>(),
                        chainState: provider.GetService<IChainState>(),
                        integrityValidator: provider.GetService<IIntegrityValidator>(),
                        partialValidator: provider.GetService<IPartialValidator>(),
                        fullValidator: provider.GetService<IFullValidator>(),
                        consensusRules: provider.GetService<IConsensusRuleEngine>(),
                        finalizedBlockInfo: provider.GetService<IFinalizedBlockInfoRepository>(),
                        signals: provider.GetService<ISignals>(),
                        peerBanning: provider.GetService<IPeerBanning>(),
                        ibdState: provider.GetService<IInitialBlockDownloadState>(),
                        chain: provider.GetService<ConcurrentChain>(),
                        blockPuller: provider.GetService<IBlockPuller>(),
                        blockStore: provider.GetService<IBlockStore>(),
                        connectionManager: provider.GetService<IConnectionManager>(),
                        nodeStats: provider.GetService<INodeStats>(),
                        nodeLifetime: provider.GetService<INodeLifetime>(),
                        consensusSettings: provider.GetService<ConsensusSettings>()
                        ));
                    services.AddSingleton<IChainedHeaderTree, ChainedHeaderTree>();
                    services.AddSingleton<IHeaderValidator, HeaderValidator>();
                    services.AddSingleton<IIntegrityValidator, IntegrityValidator>();
                    services.AddSingleton<IPartialValidator, PartialValidator>();
                    services.AddSingleton<IFullValidator, FullValidator>();

                    // Console
                    services.AddSingleton<INodeStats, NodeStats>();
                });
            });

            return fullNodeBuilder;
        }
    }
}