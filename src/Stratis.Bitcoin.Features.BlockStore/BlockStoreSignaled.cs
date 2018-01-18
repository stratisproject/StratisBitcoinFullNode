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
        private readonly AsyncQueue<ChainedBlock> blocksToAnnounce;

        /// <summary>Interval between batches in milliseconds.</summary>
        private const int batchIntervalMs = 5000;

        /// <summary>Timer that invokes <see cref="SendBatchAsync"/> when runs out.</summary>
        private readonly Timer batchTimer;

        /// <summary>Task that runs <see cref="DequeueContinuouslyAsync"/>.</summary>
        private Task dequeueTask;

        /// <summary>Set to <c>true</c> by timer when it runs out.</summary>
        private bool timerTriggered;

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

            this.blocksToAnnounce = new AsyncQueue<ChainedBlock>();

            // Configure batch timer.
            this.batchTimer = new Timer(batchIntervalMs) { AutoReset = false };
            this.batchTimer.Elapsed += (sender, args) => { this.timerTriggered = true; };
            this.timerTriggered = false;
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

            var blockPair = new BlockPair(block, chainedBlock);

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
            this.dequeueTask = this.DequeueContinuouslyAsync();
        }

        /// <summary>
        /// Continuously dequeues items from <see cref="blocksToAnnounce"/> and sends them to the peers after the timer runs out or if the last item is a tip.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There are 2 possible scenarios: 
        /// <list type="number">
        /// <item> 
        /// We've received a tip- send it right away.
        /// </item>
        /// <item>
        /// We've received a block that is behind the tip. In this case we start the timer and continue dequeuing. 
        /// Eventually the timer runs out and sets <see cref="timerTriggered"/> to <c>true</c>. But that won't trigger batch sending directly, 
        /// instead when we receive next block we check for the <see cref="timerTriggered"/> and if it's <c>true</c> we're sending the batch. 
        /// The trick here is that we can perfectly afford doing that for 2 reasons: we don't need to have a perfect interval times between the batches and,
        /// most importantly, if we've received a block that is not a tip it means that the next block (after dequeuing which we will send the batch) 
        /// will arrive shortly (a few ms) after the previous one.
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// This approach helps avoiding any locks in the code and it is preventing scenarios when two <see cref="SendBatchAsync"/> are being executed in parallel.
        /// </para>
        /// </remarks>
        private async Task DequeueContinuouslyAsync()
        {
            var batch = new List<ChainedBlock>();

            try
            {
                while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    ChainedBlock block = await this.blocksToAnnounce.DequeueAsync();

                    batch.Add(block);

                    if (this.timerTriggered || (block == this.chain.Tip))
                    {
                        this.batchTimer.Stop();
                        this.timerTriggered = false;

                        await this.SendBatchAsync(batch).ConfigureAwait(false);
                    }
                    else if (!this.batchTimer.Enabled)
                    {
                        this.batchTimer.Start();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// A method that relays blocks found in <see cref="batch"/> to connected peers on the network.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The list <see cref="batch"/> contains hashes of blocks that were validated by the consensus rules.
        /// </para>
        /// <para>
        /// These block hashes need to be relayed to connected peers. A peer that does not have a block
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
        private async Task SendBatchAsync(List<ChainedBlock> batch)
        {
            this.logger.LogTrace("()");

            int announceBlockCount = batch.Count;
            if (announceBlockCount == 0)
            {
                this.logger.LogTrace("(-)[NO_BLOCKS]");
                return;
            }

            this.logger.LogTrace("There are {0} blocks in the announce queue.", announceBlockCount);

            // Remove blocks that we've reorged away from.
            foreach (ChainedBlock reorgedBlock in batch.Where(x => this.chainState.ConsensusTip.FindAncestorOrSelf(x) == null).ToList())
            {
                this.logger.LogTrace("Block header '{0}' not found in the consensus chain and will be skipped.", reorgedBlock);

                batch.Remove(reorgedBlock);
            }

            if (!batch.Any())
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

            this.logger.LogTrace("{0} blocks will be sent to {1} peers.", batch.Count, behaviours.Count());
            foreach (BlockStoreBehavior behaviour in behaviours)
                await behaviour.AnnounceBlocksAsync(batch).ConfigureAwait(false);

            batch.Clear();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            // Let current batch sending task finish.
            this.blocksToAnnounce.Dispose();
            this.dequeueTask.GetAwaiter().GetResult();
            this.batchTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}