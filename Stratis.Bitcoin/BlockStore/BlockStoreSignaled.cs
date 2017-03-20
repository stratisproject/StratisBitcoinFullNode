using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockStore
{
    public class BlockStoreSignaled : SignalObserver<Block>
	{
		private readonly BlockStoreLoop storeLoop;
		private readonly ConcurrentChain chain;
		private readonly NodeSettings nodeArgs;
		private readonly ChainBehavior.ChainState chainState;
		private readonly ConnectionManager connection;

		private readonly ConcurrentDictionary<uint256, uint256> blockHashesToAnnounce; // maybe replace with a task scheduler


		public BlockStoreSignaled(BlockStoreLoop storeLoop, ConcurrentChain chain, NodeSettings nodeArgs, 
			BlockStore.ChainBehavior.ChainState chainState, ConnectionManager connection)
		{
			this.storeLoop = storeLoop;
			this.chain = chain;
			this.nodeArgs = nodeArgs;
			this.chainState = chainState;
			this.connection = connection;

			this.blockHashesToAnnounce = new ConcurrentDictionary<uint256, uint256>();
		}

		protected override void OnNextCore(Block value)
		{
			if (this.nodeArgs.Store.Prune)
				return;

			// ensure the block is written to disk before relaying
			this.storeLoop.AddToPending(value);

			if (this.chainState.IsInitialBlockDownload)
				return;

			this.blockHashesToAnnounce.TryAdd(value.GetHash(), value.GetHash());
		}

		public void RelayWorker(CancellationToken cancellationToken)
		{
            AsyncLoop.Run("BlockStore.RelayWorker", async token =>
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
            },
            cancellationToken,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.FiveSeconds);
		}
	}
}
