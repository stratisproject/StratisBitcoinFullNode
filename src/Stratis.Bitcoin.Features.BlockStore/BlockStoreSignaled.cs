using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreSignaled : SignalObserver<Block>
    {
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        private readonly IBlockRepository blockRepository;
        private readonly BlockStoreLoop blockStoreLoop;
        private readonly ConcurrentChain chain;
        private readonly ChainState chainState;
        private readonly IConnectionManager connection;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly string name;
        private readonly INodeLifetime nodeLifetime;
        private readonly StoreSettings storeSettings;

        private readonly ConcurrentDictionary<uint256, uint256> blockHashesToAnnounce; // maybe replace with a task scheduler

        public BlockStoreSignaled(
            BlockStoreLoop blockStoreLoop,
            ConcurrentChain chain,
            StoreSettings storeSettings,
            ChainState chainState,
            IConnectionManager connection,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            IBlockRepository blockRepository,
            ILoggerFactory loggerFactory,
            string name = "BlockStore")
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.blockHashesToAnnounce = new ConcurrentDictionary<uint256, uint256>();
            this.blockRepository = blockRepository;
            this.blockStoreLoop = blockStoreLoop;
            this.chain = chain;
            this.chainState = chainState;
            this.connection = connection;
            this.name = name;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.storeSettings = storeSettings;
        }

        protected override void OnNextCore(Block value)
        {
            this.logger.LogTrace("()");
            if (this.storeSettings.Prune)
            {
                this.logger.LogTrace("(-)[PRUNE]");
                return;
            }

            // ensure the block is written to disk before relaying
            this.blockStoreLoop.AddToPending(value);

            if (this.chainState.IsInitialBlockDownload)
            {
                this.logger.LogTrace("(-)[IBD]");
                return;
            }

            uint256 blockHash = value.GetHash();
            this.logger.LogTrace("Block hash is '{0}'.", blockHash);
            this.blockHashesToAnnounce.TryAdd(blockHash, blockHash);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A loop method that continuously relays blocks found in <see cref="blockHashesToAnnounce"/> to connected peers on the network.
        /// </summary>
        /// <remarks>
        /// The dictionary <see cref="blockHashesToAnnounce"/> contains 
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
        /// TODO: consider moving the relay logic to the <see cref="LoopSteps.ProcessPendingStorageStep"/>.
        /// </remarks>
        public void RelayWorker()
        {
            this.logger.LogTrace("()");

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.name}.RelayWorker", async token =>
            {
                this.logger.LogTrace("()");
                List<uint256> blocks = this.blockHashesToAnnounce.Keys.ToList();

                if (!blocks.Any())
                {
                    this.logger.LogTrace("(-)[NO_BLOCKS]");
                    return;
                }

                var broadcastItems = new List<uint256>();
                foreach (uint256 blockHash in blocks)
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

                if (!broadcastItems.Any())
                {
                    this.logger.LogTrace("(-)[NO_BROADCAST_ITEMS]");
                    return;
                }

                var nodes = this.connection.ConnectedNodes;
                if (!nodes.Any())
                {
                    this.logger.LogTrace("(-)[NO_NODES]");
                    return;
                }

                // Announce the blocks to each of the peers.
                var behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());
                foreach (var behaviour in behaviours)
                    await behaviour.AnnounceBlocks(blocks).ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.FiveSeconds);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// The async loop needs to complete its work before we can shut down this feature.
        /// </summary>
        internal void ShutDown()
        {
            this.asyncLoop.Dispose();
        }
    }
}