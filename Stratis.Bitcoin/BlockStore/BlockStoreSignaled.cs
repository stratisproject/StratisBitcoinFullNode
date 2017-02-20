using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.BlockStore
{
    public class BlockStoreSignaled : SignaleObserve<Block>
	{
		private readonly BlockStoreManager manager;
		private readonly ConcurrentChain chain;
		private readonly NodeArgs nodeArgs;
		private readonly ChainBehavior.ChainState chainState;
		private readonly ConnectionManager connection;

		public BlockStoreSignaled(BlockStoreManager manager, ConcurrentChain chain, NodeArgs nodeArgs, BlockStore.ChainBehavior.ChainState chainState, ConnectionManager connection)
		{
			this.manager = manager;
			this.chain = chain;
			this.nodeArgs = nodeArgs;
			this.chainState = chainState;
			this.connection = connection;
		}

		protected override void OnNextCore(Block value)
		{
			if (this.nodeArgs.Prune)
				return;

			// release the signaler from waiting 
			var task = Task.Run(async () =>
			{
				// TODO: add exception handling in this task

				// ensure the block is written to disk before relaying
				await this.manager.BlockRepository.PutAsync(value).ConfigureAwait(false);

				if (this.chainState.IsInitialBlockDownload)
					return;

				var nodes = this.connection.ConnectedNodes;
				if (!nodes.Any())
					return;

				// add the block to relay to each behaviour
				var behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());
				var hash = value.GetHash();
				foreach (var behaviour in behaviours)
					behaviour.BlockHashesToAnnounce.TryAdd(hash, hash);
			});

			// if in IBD don't wait for the store to write to disk
			// so not to slow down the IBD work, when in IBD and
			// in case of a crash the store will be able to (in future) 
			// recover itself by downloading from other peers
			if (this.chainState.IsInitialBlockDownload)
				return;

			task.GetAwaiter().GetResult(); //add the full node cancelation here.
		}
	}
}
