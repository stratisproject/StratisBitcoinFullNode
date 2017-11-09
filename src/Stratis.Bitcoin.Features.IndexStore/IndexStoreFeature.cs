using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreFeature : BlockStoreFeature
    {
        public IndexStoreFeature(ConcurrentChain chain, IConnectionManager connectionManager, Signals.Signals signals, IIndexRepository indexRepository,
            IIndexStoreCache indexStoreCache, IndexBlockPuller blockPuller, IndexStoreLoop indexStoreLoop, IndexStoreManager indexStoreManager,
            IndexStoreSignaled indexStoreSignaled, INodeLifetime nodeLifetime, NodeSettings nodeSettings, ILoggerFactory loggerFactory, IndexSettings indexSettings) :
            base(chain, connectionManager, signals, indexRepository, indexStoreCache, blockPuller, indexStoreLoop, indexStoreManager,
                indexStoreSignaled, nodeLifetime, nodeSettings, loggerFactory, indexSettings, name: "IndexStore")
        {
        }

        public override BlockStoreBehavior BlockStoreBehaviorFactory()
        {
            return new IndexStoreBehavior(this.chain, this.blockRepository as IIndexRepository, this.blockStoreCache as IIndexStoreCache, this.loggerFactory);
        }
    }

    public static class FullNodeBuilderIndexStoreExtension
    {
        public static IFullNodeBuilder UseIndexStore(this IFullNodeBuilder fullNodeBuilder, Action<IndexSettings> setup = null)
        {
            try
            {
                fullNodeBuilder.Features.EnsureFeatureRegistered<BlockStoreFeature>();
                fullNodeBuilder.Features.EnsureFeatureRegistered<MempoolFeature>();
                fullNodeBuilder.Features.EnsureFeatureRegistered<RPCFeature>();
            }
            catch (MissingDependencyException)
            {
                var logger = fullNodeBuilder.NodeSettings.LoggerFactory.CreateLogger(typeof(FullNodeBuilderIndexStoreExtension).FullName);
                logger.LogCritical($"Feature {typeof(IndexStoreFeature).Name} can not be enabled because it depends on other features that were not registered");

                return fullNodeBuilder;
            }

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<IndexStoreFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IIndexRepository, IndexRepository>();
                    services.AddSingleton<IIndexStoreCache, IndexStoreCache>();
                    services.AddSingleton<IndexBlockPuller>();
                    services.AddSingleton<IndexStoreLoop>();
                    services.AddSingleton<IndexStoreManager>();
                    services.AddSingleton<IndexStoreSignaled>();
                    services.AddSingleton<IndexStoreRPCController>();
                    services.AddSingleton<IndexSettings>(new IndexSettings(setup));
                });
            });

            return fullNodeBuilder;
        }
    }
}