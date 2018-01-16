using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using System.Timers;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreSignaled : SignalObserver<Block>
    {
        private readonly BlockStoreLoop blockStoreLoop;

        private readonly ConcurrentChain chain;

        private readonly IChainState chainState;

        private readonly IConnectionManager connection;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        
        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly StoreSettings storeSettings;

        private readonly IBlockStoreCache blockStoreCache;

        /// <summary>Queue of chained blocks that will be announced to the peers.</summary>
        private AsyncQueue<ChainedBlock> blocksToAnnounce;

        /// <summary>Local batch of blocks that will be announced to the peers.</summary>
        private readonly List<ChainedBlock> localBatch;

        /// <summary>Interval between batches.</summary>
        private readonly TimeSpan batchInterval;

        /// <summary>Timer that invokes <see cref="SendBatchLockedAsync"/> when runs out.</summary>
        private readonly Timer batchTimer;

        /// <summary>Prevents parallel execution of multiple <see cref="SendBatchLockedAsync"/> methods.</summary>
        private readonly AsyncLock asyncLock;

        public BlockStoreSignaled(
            BlockStoreLoop blockStoreLoop,
            ConcurrentChain chain,
            StoreSettings storeSettings,
            IChainState chainState,
            IConnectionManager connection,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IBlockStoreCache blockStoreCache)
        {
            this.blockStoreLoop = blockStoreLoop;
            this.chain = chain;
            this.chainState = chainState;
            this.connection = connection;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.storeSettings = storeSettings;
            this.blockStoreCache = blockStoreCache;
            this.asyncLock = new AsyncLock();

            // Set interval between batches.
            this.batchInterval = TimeSpans.Second;

            this.localBatch = new List<ChainedBlock>();

            // Configure batch timer.
            this.batchTimer = new Timer(this.batchInterval.TotalMilliseconds);
            this.batchTimer.AutoReset = false;
        }

        protected override void OnNextCore(Block block)
        {
            this.logger.LogTrace("()");
            if (this.storeSettings.Prune)
            {
                this.logger.LogTrace("(-)[PRUNE]");
                return;
            }

            ChainedBlock chainedBlock = this.chain.GetBlock(block.GetHash());
            if (chainedBlock == null)
            {
                this.logger.LogTrace("(-)[REORG]");
                return;
            }

            this.logger.LogTrace("Block hash is '{0}'.", chainedBlock.HashBlock);

            BlockPair blockPair = new BlockPair(block, chainedBlock);

            // Ensure the block is written to disk before relaying.
            this.blockStoreLoop.AddToPending(blockPair);

            if (this.blockStoreLoop.InitialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogTrace("(-)[IBD]");
                return;
            }

            // Add to cache if not in IBD.
            this.blockStoreCache.AddToCache(block);

            this.logger.LogTrace("Block header '{0}' added to the announce queue.", chainedBlock);
            this.blocksToAnnounce.Enqueue(chainedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Initializes the <see cref="BlockStoreSignaled"/>.</summary>
        public void Initialize()
        {
            this.batchTimer.Elapsed += async (sender, args) => { await this.OnBatchTimerRunsOutAsync().ConfigureAwait(false); };

            this.blocksToAnnounce = new AsyncQueue<ChainedBlock>(async (item, cancellation) =>
            {
                using (await this.asyncLock.LockAsync(cancellation).ConfigureAwait(false))
                {
                    this.localBatch.Add(item);

                    // Send batch right away if tip, if not and the timer haven't been started already- start the timer.
                    if (item == this.chain.Tip)
                    {
                        this.batchTimer.Stop();

                        await this.SendBatchLockedAsync().ConfigureAwait(false);
                    }
                    else if (!this.batchTimer.Enabled)
                    {
                        this.batchTimer.Start();
                    }
                }
            });
        }

        /// <summary><see cref="batchTimer"/>'s elapsed callback.</summary>
        private async Task OnBatchTimerRunsOutAsync()
        {
            using (await this.asyncLock.LockAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false))
            {
                if (!this.batchTimer.Enabled)
                    return;

                await this.SendBatchLockedAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// A method that relays blocks found in <see cref="localBatch"/> to connected peers on the network.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The list <see cref="localBatch"/> contains hashes of blocks that were validated by the consensus rules.
        /// </para>
        /// <para>
        /// This block hashes need to be relayed to connected peers. A peer that does not have a block
        /// will then ask for the entire block, that means only blocks that have been stored/cached should be relayed.
        /// </para>
        /// <para>
        /// During IBD blocks are not relayed to peers.
        /// </para>
        /// <para>
        /// If no nodes are connected the blocks are just discarded, however this is very unlikely to happen.
        /// </para>
        /// <para>
        /// Before relaying, verify the block is still in the best chain else discard it.
        /// </para>
        /// <para>
        /// TODO: consider moving the relay logic to the <see cref="LoopSteps.ProcessPendingStorageStep"/>.
        /// </para>
        /// </remarks>
        private async Task SendBatchLockedAsync()
        {
            this.logger.LogTrace("()");

            int announceBlockCount = this.localBatch.Count;
            if (announceBlockCount == 0)
            {
                this.logger.LogTrace("(-)[NO_BLOCKS]");
                return;
            }

            this.logger.LogTrace("There are {0} blocks in the announce queue.", announceBlockCount);

            // Initialize this list with default size of 'announceBlockCount + 4' to prevent it from autoresizing during adding new items.
            // This +4 extra size is in case new items will be added to the queue during the loop.
            var broadcastItems = new List<ChainedBlock>(announceBlockCount + 4);

            while (this.localBatch.Count > 0)
            {
                ChainedBlock block = this.localBatch.First();

                this.logger.LogTrace("Checking if block '{0}' is on disk.", block);

                // Check if we've reorged away from the current block.
                if (this.chainState.ConsensusTip.FindAncestorOrSelf(block) == null)
                {
                    this.logger.LogTrace("Block header '{0}' not found in the consensus chain.", block);

                    // Remove hash that we've reorged away from.
                    this.localBatch.Remove(block);
                    continue;
                }
               
                this.logger.LogTrace("Block '{0}' moved from the announce queue to broadcast list.", block);
                this.localBatch.Remove(block);
                broadcastItems.Add(block);
            }

            if (!broadcastItems.Any())
            {
                this.logger.LogTrace("(-)[NO_BROADCAST_ITEMS]");
                return;
            }

            IReadOnlyNetworkPeerCollection peers = this.connection.ConnectedPeers;
            if (!peers.Any())
            {
                this.logger.LogTrace("(-)[NO_PEERS]");
                return;
            }

            // Announce the blocks to each of the peers.
            IEnumerable<BlockStoreBehavior> behaviours = peers.Select(s => s.Behavior<BlockStoreBehavior>());

            this.logger.LogTrace("{0} blocks will be sent to {1} peers.", broadcastItems.Count, behaviours.Count());
            foreach (BlockStoreBehavior behaviour in behaviours)
                await behaviour.AnnounceBlocksAsync(broadcastItems).ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            this.blocksToAnnounce.Dispose();

            base.Dispose(disposing);
        }
    }
}
