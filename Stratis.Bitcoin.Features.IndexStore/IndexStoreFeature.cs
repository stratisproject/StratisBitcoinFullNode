using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Utilities;
using System;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreFeature : BlockStoreFeature
    {
        public IndexStoreFeature(ConcurrentChain chain, IConnectionManager connectionManager, Signals.Signals signals, IndexRepository indexRepository,
            IndexStoreCache indexStoreCache, IndexBlockPuller blockPuller, IndexStoreLoop indexStoreLoop, IndexStoreManager indexStoreManager,
            IndexStoreSignaled indexStoreSignaled, INodeLifetime nodeLifetime, NodeSettings nodeSettings, ILoggerFactory loggerFactory, IndexSettings indexSettings) :
            base(chain, connectionManager, signals, indexRepository, indexStoreCache, blockPuller, indexStoreLoop, indexStoreManager,
                indexStoreSignaled, nodeLifetime, nodeSettings, loggerFactory, indexSettings, name: "IndexStore")
        {
        }

        public override BlockStoreBehavior BlockStoreBehaviorFactory()
        {
            return new IndexStoreBehavior(this.chain, this.blockRepository as IndexRepository, this.blockStoreCache as IndexStoreCache, this.loggerFactory);
        }
    }

    public static class IndexStoreBuilderExtension
    {
        public static IFullNodeBuilder UseIndexStore(this IFullNodeBuilder fullNodeBuilder, Action<IndexSettings> setup = null)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<IndexStoreFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IndexRepository>();
                    services.AddSingleton<IndexStoreCache>();
                    services.AddSingleton<IndexBlockPuller>();
                    services.AddSingleton<IndexStoreLoop>();
                    services.AddSingleton<IndexStoreManager>();
                    services.AddSingleton<IndexStoreSignaled>();
                    services.AddSingleton<IndexStoreRPCController>();
                    services.AddSingleton<StoreSettings>(new IndexSettings(setup));

                });
            });

            return fullNodeBuilder;
        }
    }
}