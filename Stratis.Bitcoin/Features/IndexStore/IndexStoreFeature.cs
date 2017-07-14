using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreFeature : FullNodeFeature
    {
        private readonly ConcurrentChain chain;
        private readonly Signals.Signals signals;
        private readonly IndexRepository indexRepository;
        private readonly IndexStoreCache indexStoreCache;
        private readonly StoreBlockPuller blockPuller;
        private readonly IndexStoreLoop indexStoreLoop;
        private readonly IndexStoreManager indexStoreManager;
        private readonly IndexStoreSignaled indexStoreSignaled;
        private readonly INodeLifetime nodeLifetime;
        private readonly IConnectionManager connectionManager;
        private readonly NodeSettings nodeSettings;
        private readonly ILogger storeLogger;
        private readonly ILoggerFactory loggerFactory;

        public IndexStoreFeature(ConcurrentChain chain, IConnectionManager connectionManager, Signals.Signals signals, IndexRepository indexRepository,
            IndexStoreCache indexStoreCache, StoreBlockPuller blockPuller, IndexStoreLoop indexStoreLoop, IndexStoreManager indexStoreManager,
            IndexStoreSignaled indexStoreSignaled, INodeLifetime nodeLifetime, NodeSettings nodeSettings, ILoggerFactory loggerFactory)
        {
            this.chain = chain;
            this.signals = signals;
            this.indexRepository = indexRepository;
            this.indexStoreCache = indexStoreCache;
            this.blockPuller = blockPuller;
            this.indexStoreLoop = indexStoreLoop;
            this.indexStoreManager = indexStoreManager;
            this.indexStoreSignaled = indexStoreSignaled;
            this.nodeLifetime = nodeLifetime;
            this.connectionManager = connectionManager;
            this.nodeSettings = nodeSettings;
            this.storeLogger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
        }

        public override void Start()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(new IndexStoreBehavior(this.chain, this.indexRepository, this.indexStoreCache, this.storeLogger));
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            // signal to peers that this node can serve blocks
            this.connectionManager.Parameters.Services = (this.nodeSettings.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

            this.signals.Blocks.Subscribe(this.indexStoreSignaled);

            this.indexRepository.Initialize().GetAwaiter().GetResult();
            this.indexStoreSignaled.RelayWorker();
            this.indexStoreLoop.Initialize().GetAwaiter().GetResult();
        }

        public override void Stop()
        {
            this.storeLogger.LogInformation("Flushing IndexStore...");
            this.indexStoreManager.IndexStoreLoop.Flush().GetAwaiter().GetResult();

            this.indexStoreCache.Dispose();
            this.indexRepository.Dispose();
        }
    }

    public static class IndexStoreBuilderExtension
    {
        public static IFullNodeBuilder UseIndexStore(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<IndexStoreFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IndexRepository>();
                    services.AddSingleton<IndexStoreCache>();
                    services.AddSingleton<StoreBlockPuller>();
                    services.AddSingleton<IndexStoreLoop>();
                    services.AddSingleton<IndexStoreManager>();
                    services.AddSingleton<IndexStoreSignaled>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
