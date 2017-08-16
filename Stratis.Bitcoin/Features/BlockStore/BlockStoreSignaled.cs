using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreSignaled : SignalObserver<Block>
    {
        private readonly BlockStoreLoop storeLoop;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings nodeArgs;
        private readonly ChainState chainState;
        private readonly IConnectionManager connection;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly IBlockRepository blockRepository;
        private readonly string name;

        private readonly ConcurrentDictionary<uint256, uint256> blockHashesToAnnounce; // maybe replace with a task scheduler

        public BlockStoreSignaled(
            BlockStoreLoop storeLoop, 
            ConcurrentChain chain, 
            NodeSettings nodeArgs, 
            ChainState chainState, 
            IConnectionManager connection, 
            INodeLifetime nodeLifetime, 
            IAsyncLoopFactory asyncLoopFactory, 
            IBlockRepository blockRepository,
            string name = "BlockStore")
        {
            this.storeLoop = storeLoop;
            this.chain = chain;
            this.nodeArgs = nodeArgs;
            this.chainState = chainState;
            this.connection = connection;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockRepository = blockRepository;
            this.name = name;

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

        /// <summary>
        /// A loop method that continuously relays blocks found in <see cref="BlockStoreSignaled.blockHashesToAnnounce"/> to connected peers on the network.
        /// </summary>
        /// <remarks>
        /// The dictionary <see cref="BlockStoreSignaled.blockHashesToAnnounce"/> contains 
        /// hashes of blocks that were validated by the consensus rules.
        /// 
        /// This block hashes need to be relayed to connected peers. A peer that does not have a block 
        /// will then ask for the entire block, that means only blocks that have been stored should be relayed.
        /// 
        /// During IBD blocks are not relayed to peers.
        /// 
        /// If no nodes are connected the blocks are just discarded, however this is very unlikely to happen.
        /// 
        /// Before relaying, verify the block is still in the best chain else discard it.
        /// 
        /// TODO: consider moving the relay logic to the <see cref="ProcessPendingStorageStep.PushPendingBlocksToRepository"/>.
        /// </remarks>
        public void RelayWorker()
        {
            this.asyncLoopFactory.Run($"{this.name}.RelayWorker", async token =>
            {
                var blocks = this.blockHashesToAnnounce.Keys.ToList();

                if (!blocks.Any())
                    return;
                
                var broadcastItems = new List<uint256>();
                foreach (var blockHash in blocks)
                {
                    // The first block that is not in disk will abort the loop.
                    if (!await this.blockRepository.ExistAsync(blockHash).ConfigureAwait(false))
                    {
                        // NOTE: there is a very minimal possibility a reorg would happen 
                        // and post reorg blocks will now be in the 'blockHashesToAnnounce', 
                        // current logic will discard those blocks, I suspect this will be unlikely 
                        // to happen. In case it does a block will just not be relays.

                        // If the hash is not on disk and not in the best chain 
                        // we assume all the following blocks are also discardable
                        if (!this.chain.Contains(blockHash))
                            this.blockHashesToAnnounce.Clear();

                        break;
                    }

                    if (this.blockHashesToAnnounce.TryRemove(blockHash, out uint256 hashToBroadcast))
                        broadcastItems.Add(hashToBroadcast);
                }

                if(!broadcastItems.Any())
                    return;

                var nodes = this.connection.ConnectedNodes;
                if (!nodes.Any())
                    return;

                // Announce the blocks to each of the peers.
                var behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());
                foreach (var behaviour in behaviours)
                    await behaviour.AnnounceBlocks(blocks).ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.FiveSeconds);
        }
    }
}
