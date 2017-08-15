using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.Deployments;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

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
    public class BaseFeature : FullNodeFeature
    {
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Disposable resources that will be disposed when the feature stops.</summary>
        private readonly List<IDisposable> disposableResources = new List<IDisposable>();

        /// <summary>Information about node's chain.</summary>
        private readonly ChainState chainState;

        /// <summary>Access to the database of blocks.</summary>
        private readonly ChainRepository chainRepository;

        /// <summary>User defined node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Locations of important folders and files on disk.</summary>
        private readonly DataFolder dataFolder;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Manager of node's network connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Periodic task to save the chain to the database.</summary>
        private PeriodicTask flushChainTask;

        /// <summary>Periodic task to save list of peers to disk.</summary>
        private PeriodicTask flushAddressManagerTask;

        /// <summary>Manager of node's network peers.</summary>
        private AddressManager addressManager;

        /// <summary>Logger for the node.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="dataFolder">Locations of important folders and files on disk.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="nodeLifetime">Global application life cycle control - triggers when application shuts down.</param>
        /// <param name="chain">Thread safe chain of block headers from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="connectionManager">Manager of node's network connections.</param>
        /// <param name="chainRepository">Access to the database of blocks.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        public BaseFeature(
            NodeSettings nodeSettings,
            DataFolder dataFolder,
            Network network,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain,
            ChainState chainState,
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

        /// <inheritdoc />
        public override void Start()
        {
            this.StartAddressManager();
            this.StartChain();

            var connectionParameters = this.connectionManager.Parameters;
            connectionParameters.IsRelay = this.nodeSettings.Mempool.RelayTxes;
            connectionParameters.TemplateBehaviors.Add(new ChainHeadersBehavior(this.chain, this.chainState));
            connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(this.addressManager));

            this.disposableResources.Add(this.nodeSettings.LoggerFactory);
            this.disposableResources.Add(this.chainRepository);
            this.disposableResources.Add(this.connectionManager);
        }

        /// <summary>
        /// Initializes node's chain repository.
        /// Creates periodic task to persist changes to the database.
        /// </summary>
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

        /// <summary>
        /// Initializes node's address manager. Loads previously known peers from the file 
        /// or creates new peer file if it does not exist. Creates periodic task to persist changes 
        /// in peers to disk.
        /// </summary>
        private void StartAddressManager()
        {
            if (!File.Exists(this.dataFolder.AddrManFile))
            {
                this.logger.LogInformation($"Creating {this.dataFolder.AddrManFile}");
                this.addressManager = new AddressManager();
                this.addressManager.SavePeerFile(this.dataFolder.AddrManFile, this.network);
                this.logger.LogInformation("Created");
            }
            else
            {
                this.logger.LogInformation($"Loading  {this.dataFolder.AddrManFile}");
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

        /// <inheritdoc />
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


    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
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
                    services.AddSingleton(fullNodeBuilder.NodeSettings.LoggerFactory);
                    services.AddSingleton(fullNodeBuilder.NodeSettings.DataFolder);
                    services.AddSingleton<INodeLifetime, NodeLifetime>();
                    services.AddSingleton<FullNodeFeatureExecutor>();
                    services.AddSingleton<Signals.Signals>().AddSingleton<ISignals, Signals.Signals>(provider => provider.GetService<Signals.Signals>());
                    services.AddSingleton<FullNode>().AddSingleton((provider) => { return provider.GetService<FullNode>() as IFullNode; });
                    services.AddSingleton<ConcurrentChain>(new ConcurrentChain(fullNodeBuilder.Network));
                    services.AddSingleton<IDateTimeProvider>(DateTimeProvider.Default);
                    services.AddSingleton<ChainState>();
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