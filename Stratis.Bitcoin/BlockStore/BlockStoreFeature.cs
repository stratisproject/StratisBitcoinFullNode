using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;

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
		private readonly FullNode.CancellationProvider cancellationProvider;
		private readonly ConnectionManager connectionManager;

		public BlockStoreFeature(ConcurrentChain chain, ConnectionManager connectionManager, Signals signals, BlockRepository blockRepository,  
			BlockStoreCache blockStoreCache, StoreBlockPuller blockPuller, BlockStoreLoop blockStoreLoop, BlockStoreManager blockStoreManager,
			BlockStoreSignaled blockStoreSignaled, FullNode.CancellationProvider cancellationProvider)
		{
			this.chain = chain;
			this.signals = signals;
			this.blockRepository = blockRepository;
			this.blockStoreCache = blockStoreCache;
			this.blockPuller = blockPuller;
			this.blockStoreLoop = blockStoreLoop;
			this.blockStoreManager = blockStoreManager;
			this.blockStoreSignaled = blockStoreSignaled;
			this.cancellationProvider = cancellationProvider;
			this.connectionManager = connectionManager;
		}

		public override void Start()
		{
			this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache));
			this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPuller.BlockPullerBehavior(this.blockPuller));
			this.signals.Blocks.Subscribe(this.blockStoreSignaled);

			this.blockStoreSignaled.RelayWorker(this.cancellationProvider.Cancellation.Token);
			this.blockStoreLoop.Initialize(this.cancellationProvider.Cancellation).GetAwaiter().GetResult();
		}

		public override void Stop()
		{
			this.blockStoreCache.Dispose();
			this.blockRepository.Dispose();
		}
	}

	public static class MBlockStoreBuilderExtension
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
