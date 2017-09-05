﻿using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.BlockStore.Tests")]

namespace Stratis.Bitcoin.Features.BlockStore
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NBitcoin;
    using NBitcoin.Protocol;
    using Stratis.Bitcoin.BlockPulling;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Builder.Feature;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Configuration.Logging;
    using Stratis.Bitcoin.Connection;
    using Stratis.Bitcoin.Interfaces;
    using Stratis.Bitcoin.Utilities;
    using System;
    using System.Threading.Tasks;

    public class BlockStoreFeature : FullNodeFeature, IBlockStore
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
            storeSettings.Load(nodeSettings);
            this.storeSettings = storeSettings;
        }

        public virtual BlockStoreBehavior BlockStoreBehaviorFactory()
        {
            return new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.loggerFactory);
        }

        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            return this.blockRepository.GetTrxAsync(trxid);
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            return this.blockRepository.GetTrxBlockIdAsync(trxid);
        }

        public override void Start()
        {
            this.logger.LogTrace("()");

            this.connectionManager.Parameters.TemplateBehaviors.Add(BlockStoreBehaviorFactory());
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            // signal to peers that this node can serve blocks
            this.connectionManager.Parameters.Services = (this.storeSettings.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

            this.signals.SubscribeForBlocks(this.blockStoreSignaled);

            this.blockRepository.Initialize().GetAwaiter().GetResult();
            this.blockStoreSignaled.RelayWorker();
            this.blockStoreLoop.Initialize().GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        public override void Stop()
        {
            this.logger.LogTrace("()");

            this.logger.LogInformation("Flushing {0}...", this.name);
            this.blockStoreManager.BlockStoreLoop.Flush().GetAwaiter().GetResult();

            this.blockStoreCache.Dispose();
            this.blockRepository.Dispose();

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
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
                        services.AddSingleton<BlockStoreManager>();
                        services.AddSingleton<BlockStoreSignaled>();
                        services.AddSingleton<StoreSettings>(new StoreSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }
    }
}