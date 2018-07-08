﻿using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
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
    public class BlockStoreFeature : FullNodeFeature, INodeStats, IFeatureStats
    {
        private readonly ConcurrentChain chain;

        private readonly Signals.Signals signals;

        private readonly IBlockRepository blockRepository;

        private readonly IBlockStoreCache blockStoreCache;

        private readonly BlockStoreQueue blockStoreQueue;

        private readonly BlockStoreManager blockStoreManager;

        private readonly BlockStoreSignaled blockStoreSignaled;

        private readonly INodeLifetime nodeLifetime;

        private readonly IConnectionManager connectionManager;

        private readonly NodeSettings nodeSettings;

        private readonly StoreSettings storeSettings;

        private readonly IChainState chainState;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        private readonly string name;

        public BlockStoreFeature(
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            Signals.Signals signals,
            IBlockRepository blockRepository,
            IBlockStoreCache blockStoreCache,
            BlockStoreQueue blockStoreQueue,
            BlockStoreManager blockStoreManager,
            BlockStoreSignaled blockStoreSignaled,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            StoreSettings storeSettings,
            IChainState chainState,
            string name = "BlockStore")
        {
            this.name = name;
            this.chain = chain;
            this.signals = signals;
            this.blockRepository = blockRepository;
            this.blockStoreCache = blockStoreCache;
            this.blockStoreQueue = blockStoreQueue;
            this.blockStoreManager = blockStoreManager;
            this.blockStoreSignaled = blockStoreSignaled;
            this.nodeLifetime = nodeLifetime;
            this.connectionManager = connectionManager;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.storeSettings = storeSettings;
            this.chainState = chainState;
        }

        public virtual BlockStoreBehavior BlockStoreBehaviorFactory()
        {
            return new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.loggerFactory);
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            ChainedHeader highestBlock = this.chainState.BlockStoreTip;

            if (highestBlock != null)
            {
                benchLogs.AppendLine($"{this.name}.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                     highestBlock.Height.ToString().PadRight(8) +
                                     $" {this.name}.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                                     highestBlock.HashBlock);
            }
        }

        /// <inheritdoc />
        public void AddFeatureStats(StringBuilder benchLog)
        {
            this.blockStoreQueue.ShowStats(benchLog);
        }

        public override void Initialize()
        {
            this.logger.LogTrace("()");

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.BlockStoreBehaviorFactory());

            // signal to peers that this node can serve blocks
            this.connectionManager.Parameters.Services = (this.storeSettings.Prune ? NetworkPeerServices.Nothing : NetworkPeerServices.Network) | NetworkPeerServices.NODE_WITNESS;

            this.signals.SubscribeForBlocks(this.blockStoreSignaled);

            this.blockRepository.InitializeAsync().GetAwaiter().GetResult();
            this.blockStoreQueue.InitializeAsync().GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            StoreSettings.PrintHelp();
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
            this.blockStoreManager.BlockStoreQueue.Dispose();
            this.blockRepository.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderBlockStoreExtension
    {
        public static IFullNodeBuilder UseBlockStore(this IFullNodeBuilder fullNodeBuilder)
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
                        services.AddSingleton<BlockStoreQueue>().AddSingleton<IBlockStore, BlockStoreQueue>(provider => provider.GetService<BlockStoreQueue>());
                        services.AddSingleton<BlockStoreManager>();
                        services.AddSingleton<BlockStoreSignaled>();
                        services.AddSingleton<StoreSettings>();
                        services.AddSingleton<BlockStoreController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
