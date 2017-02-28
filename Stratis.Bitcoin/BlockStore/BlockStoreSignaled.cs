using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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

		private readonly ConcurrentDictionary<uint256, uint256> blockHashesToAnnounce; // maybe replace with a task scheduler


		public BlockStoreSignaled(BlockStoreManager manager, ConcurrentChain chain, NodeArgs nodeArgs, BlockStore.ChainBehavior.ChainState chainState, ConnectionManager connection, CancellationTokenSource globalCancellationTokenSource)
		{
			this.manager = manager;
			this.chain = chain;
			this.nodeArgs = nodeArgs;
			this.chainState = chainState;
			this.connection = connection;

			this.blockHashesToAnnounce = new ConcurrentDictionary<uint256, uint256>();
			this.RelayWorker(globalCancellationTokenSource.Token);
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
				
				this.blockHashesToAnnounce.TryAdd(value.GetHash(), value.GetHash());
			});

			// if in IBD don't wait for the store to write to disk
			// so not to slow down the IBD work, when in IBD and
			// in case of a crash the store will be able to (in future) 
			// recover itself by downloading from other peers
			if (this.chainState.IsInitialBlockDownload)
				return;

			task.GetAwaiter().GetResult(); //add the full node cancelation here.
		}

		private void RelayWorker(CancellationToken cancellationToken)
		{
			new PeriodicAsyncTask("BlockStore.RelayWorker", async token =>
			{
				var blocks = this.blockHashesToAnnounce.Keys.ToList();

				if (!blocks.Any())
					return;

				uint256 outer;
				foreach (var blockHash in blocks)
					this.blockHashesToAnnounce.TryRemove(blockHash, out outer);

				var nodes = this.connection.ConnectedNodes;
				if (!nodes.Any())
					return;

				// announce the blocks on each nodes behaviour
				var behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());
				foreach (var behaviour in behaviours)
					await behaviour.AnnounceBlocks(blocks).ConfigureAwait(false);

			}).StartAsync(cancellationToken, TimeSpan.FromMilliseconds(1000), true);
		}
	}
}
