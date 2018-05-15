using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.BlockStore.Tests")]

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreFeature : FullNodeFeature, IBlockStore, INodeStats
    {
        protected readonly ConcurrentChain chain;

        protected readonly Signals.Signals signals;

        protected readonly IBlockRepository blockRepository;

        protected readonly IBlockStoreCache blockStoreCache;

        protected readonly StoreBlockPuller blockPuller;

        protected readonly BlockStoreLoop blockStoreLoop;

        protected readonly BlockStoreManager blockStoreManager;

        protected readonly BlockStoreSignaled blockStoreSignaled;

        protected readonly INodeLifetime nodeLifetime;

        protected readonly IConnectionManager connectionManager;

        protected readonly NodeSettings nodeSettings;

        protected readonly StoreSettings storeSettings;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        protected readonly string name;

        public BlockStoreFeature(
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            Signals.Signals signals,
            IBlockRepository blockRepository,
            IBlockStoreCache blockStoreCache,
            StoreBlockPuller blockPuller,
            BlockStoreLoop blockStoreLoop,
            BlockStoreManager blockStoreManager,
            BlockStoreSignaled blockStoreSignaled,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            StoreSettings storeSettings,
            string name = "BlockStore")
        {
            this.name = name;
            this.chain = chain;
            this.signals = signals;
            this.blockRepository = blockRepository;
            this.blockStoreCache = blockStoreCache;
            this.blockPuller = blockPuller;
            this.blockStoreLoop = blockStoreLoop;
            this.blockStoreManager = blockStoreManager;
            this.blockStoreSignaled = blockStoreSignaled;
            this.nodeLifetime = nodeLifetime;
            this.connectionManager = connectionManager;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.storeSettings = storeSettings;
        }

        /// <inheritdoc />
        public override void LoadConfiguration()
        {
            this.storeSettings.Load(this.nodeSettings);
        }

        public virtual BlockStoreBehavior BlockStoreBehaviorFactory()
        {
            return new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.loggerFactory);
        }

        public void AddNodeStats(StringBuilder benchLogs)
        {
            var highestBlock = this.blockRepository.HighestPersistedBlock;

            if (highestBlock != null)
                benchLogs.AppendLine($"{this.name}.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                    highestBlock.Height.ToString().PadRight(8) +
                    $" {this.name}.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                    highestBlock.HashBlock);
        }

        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            return this.blockRepository.GetTrxAsync(trxid);
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            return this.blockRepository.GetTrxBlockIdAsync(trxid);
        }

        public override void Initialize()
        {
            this.logger.LogTrace("()");

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.BlockStoreBehaviorFactory());
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            // signal to peers that this node can serve blocks
            this.connectionManager.Parameters.Services = (this.storeSettings.Prune ? NetworkPeerServices.Nothing : NetworkPeerServices.Network) | NetworkPeerServices.NODE_WITNESS;

            this.signals.SubscribeForBlocks(this.blockStoreSignaled);

            this.blockRepository.InitializeAsync().GetAwaiter().GetResult();
            this.blockStoreLoop.InitializeAsync().GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            StoreSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            StoreSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.logger.LogInformation("Stopping {0}...", this.name);

            this.blockStoreSignaled.Dispose();
            this.blockStoreManager.BlockStoreLoop.ShutDown();
            this.blockRepository.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderBlockStoreExtension
    {
        public static IFullNodeBuilder UseBlockStore(this IFullNodeBuilder fullNodeBuilder, Action<StoreSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<BlockStoreFeature>("db");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BlockStoreFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<IBlockRepository, BlockRepository>();
                        services.AddSingleton<IBlockStoreCache, BlockStoreCache>();
                        services.AddSingleton<StoreBlockPuller>();
                        services.AddSingleton<BlockStoreLoop>();
                        services.AddSingleton<IStoreStateProvider>(x => x.GetService<BlockStoreLoop>());
                        services.AddSingleton<BlockStoreManager>();
                        services.AddSingleton<BlockStoreSignaled>();
                        services.AddSingleton<StoreSettings>(new StoreSettings(setup));
                        services.AddSingleton<BlockStoreController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
