﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
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

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly StoreSettings storeSettings;

        /// <summary>Queue of chained blocks that will be announced to the peers.</summary>
        private readonly ConcurrentQueue<ChainedBlock> blocksToAnnounce;

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
            this.blocksToAnnounce = new ConcurrentQueue<ChainedBlock>();
            this.blockRepository = blockRepository;
            this.blockStoreLoop = blockStoreLoop;
            this.chain = chain;
            this.chainState = chainState;
            this.connection = connection;
            this.name = name;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.storeSettings = storeSettings;
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

            if (this.chainState.IsInitialBlockDownload)
            {
                this.logger.LogTrace("(-)[IBD]");
                return;
            }

            this.logger.LogTrace("Block header '{0}' added to the announce queue.", chainedBlock);
            this.blocksToAnnounce.Enqueue(chainedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A loop method that continuously relays blocks found in <see cref="blocksToAnnounce"/> to connected peers on the network.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The queue <see cref="blocksToAnnounce"/> contains
        /// hashes of blocks that were validated by the consensus rules.
        /// </para>
        /// <para>
        /// This block hashes need to be relayed to connected peers. A peer that does not have a block
        /// will then ask for the entire block, that means only blocks that have been stored should be relayed.
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
        /// TODO: consider moving the relay logic to the <see cref="LoopSteps.ProcessPendingStorageStep"/>.
        /// </remarks>
        public void RelayWorker()
        {
            this.logger.LogTrace("()");

            this.asyncLoop = this.asyncLoopFactory.Run($"{this.name}.RelayWorker", async token =>
            {
                this.logger.LogTrace("()");

                int announceBlockCount = this.blocksToAnnounce.Count;
                if (announceBlockCount == 0)
                {
                    this.logger.LogTrace("(-)[NO_BLOCKS]");
                    return;
                }

                this.logger.LogTrace("There are {0} blocks in the announce queue.", announceBlockCount);

                // Initialize this list with default size of 'announceBlockCount + 4' to prevent it from autoresizing during adding new items.
                // This +4 extra size is in case new items will be added to the queue during the loop.
                var broadcastItems = new List<ChainedBlock>(announceBlockCount + 4);

                while (this.blocksToAnnounce.TryPeek(out ChainedBlock block))
                {
                    this.logger.LogTrace("Checking if block '{0}' is on disk.", block);

                    // The first block that is not on disk will abort the loop.
                    if (!await this.blockRepository.ExistAsync(block.HashBlock).ConfigureAwait(false))
                    {
                        this.logger.LogTrace("Block '{0}' not found in the store.", block);

                        // In cases when the node had a reorg the 'blocksToAnnounce' contain blocks
                        // that are not anymore on the main chain, those blocks are removed from 'blocksToAnnounce'.

                        // Check if the reason why we don't have a block is a reorg or it hasn't been downloaded yet.
                        if (this.chainState.ConsensusTip.FindAncestorOrSelf(block) == null)
                        {
                            this.logger.LogTrace("Block header '{0}' not found in the consensus chain.", block);

                            // Remove hash that we've reorged away from.
                            this.blocksToAnnounce.TryDequeue(out ChainedBlock unused);
                            continue;
                        }
                        else this.logger.LogTrace("Block header '{0}' found in the consensus chain, will wait until it is stored on disk.", block);

                        break;
                    }

                    if (this.blocksToAnnounce.TryDequeue(out ChainedBlock blockToBroadcast))
                    {
                        this.logger.LogTrace("Block '{0}' moved from the announce queue to broadcast list.", block);
                        broadcastItems.Add(blockToBroadcast);
                    }
                    else this.logger.LogTrace("Unable to removing block '{0}' from the announce queue.", block);
                }

                if (!broadcastItems.Any())
                {
                    this.logger.LogTrace("(-)[NO_BROADCAST_ITEMS]");
                    return;
                }

                IReadOnlyNetworkPeerCollection nodes = this.connection.ConnectedNodes;
                if (!nodes.Any())
                {
                    this.logger.LogTrace("(-)[NO_NODES]");
                    return;
                }

                // Announce the blocks to each of the peers.
                IEnumerable<BlockStoreBehavior> behaviours = nodes.Select(s => s.Behavior<BlockStoreBehavior>());

                this.logger.LogTrace("{0} blocks will be sent to {1} peers.", broadcastItems.Count, behaviours.Count());
                foreach (BlockStoreBehavior behaviour in behaviours)
                    await behaviour.AnnounceBlocksAsync(broadcastItems).ConfigureAwait(false);
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
