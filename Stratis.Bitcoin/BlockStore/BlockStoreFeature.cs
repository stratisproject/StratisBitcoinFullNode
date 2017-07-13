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
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.BlockStore
{
	public class BlockStoreFeature : FullNodeFeature 
	{
		private readonly ConcurrentChain chain;
		private readonly Signals signals;
		private readonly BlockRepository blockRepository;
		private readonly BlockStoreCache blockStoreCache;
		private readonly StoreBlockPuller blockPuller;
		private readonly BlockStoreLoop blockStoreLoop;
		private readonly BlockStoreManager blockStoreManager;
		private readonly BlockStoreSignaled blockStoreSignaled;
		private readonly INodeLifetime nodeLifetime;
		private readonly IConnectionManager connectionManager;
		private readonly NodeSettings nodeSettings;
	    private readonly ILogger storeLogger;

        public BlockStoreFeature(ConcurrentChain chain, IConnectionManager connectionManager, Signals signals, BlockRepository blockRepository,  
			BlockStoreCache blockStoreCache, StoreBlockPuller blockPuller, BlockStoreLoop blockStoreLoop, BlockStoreManager blockStoreManager,
			BlockStoreSignaled blockStoreSignaled, INodeLifetime nodeLifetime, NodeSettings nodeSettings, ILoggerFactory loggerFactory)
		{
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
		    this.storeLogger = loggerFactory.CreateLogger(this.GetType().FullName);
		}

		public override void Start()
		{
			this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.storeLogger));
			this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPuller.BlockPullerBehavior(this.blockPuller));

            // signal to peers that this node can serve blocks
            this.connectionManager.Parameters.Services = (this.nodeSettings.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

            this.signals.Blocks.Subscribe(this.blockStoreSignaled);

			this.blockRepository.Initialize().GetAwaiter().GetResult();
			this.blockStoreSignaled.RelayWorker();
			this.blockStoreLoop.Initialize().GetAwaiter().GetResult();			
		}

		public override void Stop()
		{
		    this.storeLogger.LogInformation("Flushing BlockStore...");
			this.blockStoreManager.BlockStoreLoop.Flush().GetAwaiter().GetResult();

			this.blockStoreCache.Dispose();
			this.blockRepository.Dispose();
		}
    }

	public static class BlockStoreBuilderExtension
	{
		public static IFullNodeBuilder UseBlockStore(this IFullNodeBuilder fullNodeBuilder)
		{          
            fullNodeBuilder.ConfigureFeature(features =>
			{
				features
				.AddFeature<BlockStoreFeature>()
				.FeatureServices(services =>
					{
						services.AddSingleton<BlockRepository>();
						services.AddSingleton<BlockStoreCache>();
						services.AddSingleton<StoreBlockPuller>();
						services.AddSingleton<BlockStoreLoop>();
						services.AddSingleton<BlockStoreManager>();
						services.AddSingleton<BlockStoreSignaled>();
					});
			});

			return fullNodeBuilder;
		}
	}
}
