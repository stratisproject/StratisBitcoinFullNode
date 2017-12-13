﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly StoreSettings storeSettings;

        private readonly ConcurrentQueue<uint256> blockHashesToAnnounce;

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
            this.blockHashesToAnnounce = new ConcurrentQueue<uint256>();
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

            // ensure the block is written to disk before relaying
            this.blockStoreLoop.AddToPending(block);

            if (this.chainState.IsInitialBlockDownload)
            {
                this.logger.LogTrace("(-)[IBD]");
                return;
            }

            uint256 blockHash = block.GetHash();
            this.logger.LogTrace("Block hash is '{0}'.", blockHash);
            this.blockHashesToAnnounce.Enqueue(blockHash);

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

                if (!this.blockHashesToAnnounce.Any())
                {
                    this.logger.LogTrace("(-)[NO_BLOCKS]");
                    return;
                }

                var broadcastItems = new List<uint256>();

                while (this.blockHashesToAnnounce.TryPeek(out uint256 blockHash))
                {
                    // The first block that is not in disk will abort the loop.
                    if (!await this.blockRepository.ExistAsync(blockHash).ConfigureAwait(false))
                    {
                        // There is a small possibility that a reorg happened and 'blockHashesToAnnounce'
                        // contains hashes of blocks that we've reorged away from.
                        // Current logic will discard all blocks waiting for being announced if we've encountered a block that we've reorged from.

                        // If the hash is not on disk and not in the best chain we assume all the following blocks are also discardable.
                        if (!this.chain.Contains(blockHash))
                        {
                            // Clear queue.
                            while (this.blockHashesToAnnounce.TryDequeue(out uint256 item))
                            {
                            }
                        }

                        break;
                    }

                    if (this.blockHashesToAnnounce.TryDequeue(out uint256 hashToBroadcast))
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
                    await behaviour.AnnounceBlocks(broadcastItems).ConfigureAwait(false);
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
