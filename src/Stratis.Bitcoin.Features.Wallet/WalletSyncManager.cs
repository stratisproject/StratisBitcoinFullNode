﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager, IDisposable
    {
        private readonly IWalletManager walletManager;

        private readonly ChainIndexer chainIndexer;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBlockStore blockStore;

        private readonly StoreSettings storeSettings;

        private readonly ISignals signals;
        private readonly IAsyncProvider asyncProvider;
        protected ChainedHeader walletTip;

        public ChainedHeader WalletTip => this.walletTip;

        /// <summary>Queue which contains blocks that should be processed by <see cref="WalletManager"/>.</summary>
        private readonly IAsyncDelegateDequeuer<Block> blocksQueue;

        /// <summary>Current <see cref="blocksQueue"/> size in bytes.</summary>
        private long blocksQueueSize;

        /// <summary>Flag to determine when the <see cref="MaxQueueSize"/> is reached.</summary>
        private bool maxQueueSizeReached;

        private SubscriptionToken blockConnectedSubscription;
        private SubscriptionToken transactionReceivedSubscription;

        /// <summary>Limit <see cref="blocksQueue"/> size to 100MB.</summary>
        private const int MaxQueueSize = 100 * 1024 * 1024;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ChainIndexer chainIndexer,
            Network network, IBlockStore blockStore, StoreSettings storeSettings, ISignals signals, IAsyncProvider asyncProvider)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStore, nameof(blockStore));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));

            this.walletManager = walletManager;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.storeSettings = storeSettings;
            this.signals = signals;
            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.blocksQueue = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<Block>($"{nameof(WalletSyncManager)}-{nameof(this.blocksQueue)}", this.OnProcessBlockAsync);

            this.blocksQueueSize = 0;
        }

        /// <inheritdoc />
        public void Start()
        {
            // When a node is pruned it impossible to catch up
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be
            // able to download blocks from peers to catch up.
            if (this.storeSettings.PruningEnabled)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.", this.walletManager.LastBlockHeight());

            this.walletTip = this.chainIndexer.GetHeader(this.walletManager.WalletTipHash);
            if (this.walletTip == null)
            {
                // The wallet tip was not found in the main chain.
                // this can happen if the node crashes unexpectedly.
                // To recover we need to find the first common fork
                // with the best chain. As the wallet does not have a
                // list of chain headers, we use a BlockLocator and persist
                // that in the wallet. The block locator will help finding
                // a common fork and bringing the wallet back to a good
                // state (behind the best chain).
                ICollection<uint256> locators = this.walletManager.ContainsWallets ? this.walletManager.GetFirstWalletBlockLocator() : new[] { this.chainIndexer.Tip.HashBlock };
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chainIndexer.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                this.walletManager.WalletTipHash = fork.HashBlock;
                this.walletTip = fork;
            }

            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(this.OnTransactionAvailable);
        }

        private void OnTransactionAvailable(TransactionReceived transactionReceived)
        {
            this.ProcessTransaction(transactionReceived.ReceivedTransaction);
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            this.ProcessBlock(blockConnected.ConnectedBlock.Block);
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.signals.Unsubscribe(this.blockConnectedSubscription);
            this.signals.Unsubscribe(this.transactionReceivedSubscription);
        }

        /// <summary>Called when a <see cref="Block"/> is added to the <see cref="blocksQueue"/>.
        /// Depending on the <see cref="WalletTip"/> and incoming block height, this method will decide whether the <see cref="Block"/> will be processed by the <see cref="WalletManager"/>.
        /// </summary>
        /// <param name="block">Block to be processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task OnProcessBlockAsync(Block block, CancellationToken cancellationToken)
        {
            Guard.NotNull(block, nameof(block));

            long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, -block.BlockSize.Value);
            this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

            ChainedHeader newTip = this.chainIndexer.GetHeader(block.GetHash());

            if (newTip == null)
            {
                this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                return;
            }

            // If the new block's previous hash is not the same as the one we have, there might have been a reorg.
            // If the new block follows the previous one, just pass the block to the manager.
            if (block.Header.HashPrevBlock != this.walletTip.HashBlock)
            {
                // If previous block does not match there might have
                // been a reorg, check if the wallet is still on the main chain.
                ChainedHeader inBestChain = this.chainIndexer.GetHeader(this.walletTip.HashBlock);
                if (inBestChain == null)
                {
                    // The current wallet hash was not found on the main chain.
                    // A reorg happened so bring the wallet back top the last known fork.
                    ChainedHeader fork = this.walletTip;

                    // We walk back the chained block object to find the fork.
                    while (this.chainIndexer.GetHeader(fork.HashBlock) == null)
                        fork = fork.Previous;

                    this.logger.LogInformation("Reorg detected, going back from '{0}' to '{1}'.", this.walletTip, fork);

                    this.walletManager.RemoveBlocks(fork);
                    this.walletTip = fork;

                    this.logger.LogTrace("Wallet tip set to '{0}'.", this.walletTip);
                }

                // The new tip can be ahead or behind the wallet.
                // If the new tip is ahead we try to bring the wallet up to the new tip.
                // If the new tip is behind we just check the wallet and the tip are in the same chain.
                if (newTip.Height > this.walletTip.Height)
                {
                    ChainedHeader findTip = newTip.FindAncestorOrSelf(this.walletTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_AHEAD_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogTrace("Wallet tip '{0}' is behind the new tip '{1}'.", this.walletTip, newTip);

                    ChainedHeader next = this.walletTip;
                    while (next != newTip)
                    {
                        // While the wallet is catching up the entire node will wait.
                        // If a wallet is recovered to a date in the past. Consensus will stop until the wallet is up to date.

                        // TODO: This code should be replaced with a different approach
                        // Similar to BlockStore the wallet should be standalone and not depend on consensus.
                        // The block should be put in a queue and pushed to the wallet in an async way.
                        // If the wallet is behind it will just read blocks from store (or download in case of a pruned node).

                        next = newTip.GetAncestor(next.Height + 1);
                        Block nextblock = null;
                        int index = 0;
                        while (true)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                this.logger.LogTrace("(-)[CANCELLATION_REQUESTED]");
                                return;
                            }

                            nextblock = this.blockStore.GetBlock(next.HashBlock);
                            if (nextblock == null)
                            {
                                // The idea in this abandoning of the loop is to release consensus to push the block.
                                // That will make the block available in the next push from consensus.
                                index++;
                                if (index > 10)
                                {
                                    this.logger.LogTrace("(-)[WALLET_CATCHUP_INDEX_MAX]");
                                    return;
                                }

                                // Really ugly hack to let store catch up.
                                // This will block the entire consensus pulling.
                                this.logger.LogWarning("Wallet is behind the best chain and the next block is not found in store.");
                                Thread.Sleep(100);
                                continue;
                            }

                            break;
                        }

                        this.walletTip = next;
                        this.walletManager.ProcessBlock(nextblock, next);
                    }
                }
                else
                {
                    ChainedHeader findTip = this.walletTip.FindAncestorOrSelf(newTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_BEHIND_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.walletTip, newTip);
                }
            }
            else this.logger.LogTrace("New block follows the previously known block '{0}'.", this.walletTip);

            this.walletTip = newTip;
            this.walletManager.ProcessBlock(block, newTip);
        }

        /// <inheritdoc />
        public virtual void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            if (!this.walletManager.ContainsWallets)
            {
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }

            // If the queue reaches the maximum limit, ignore incoming blocks until the queue is empty.
            if (!this.maxQueueSizeReached)
            {
                if (this.blocksQueueSize >= MaxQueueSize)
                {
                    this.maxQueueSizeReached = true;
                    this.logger.LogTrace("(-)[REACHED_MAX_QUEUE_SIZE]");
                    return;
                }
            }
            else
            {
                // If queue is empty then reset the maxQueueSizeReached flag.
                this.maxQueueSizeReached = this.blocksQueueSize > 0;
            }

            if (!this.maxQueueSizeReached)
            {
                long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, block.BlockSize.Value);
                this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

                this.blocksQueue.Enqueue(block);
            }
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            this.walletManager.ProcessTransaction(transaction);
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date)
        {
            int blockSyncStart = this.chainIndexer.GetHeightAtTime(date);
            this.SyncFromHeight(blockSyncStart);
        }

        /// <inheritdoc />
        public virtual void SyncFromHeight(int height)
        {
            ChainedHeader chainedHeader = this.chainIndexer.GetHeader(height);
            this.walletTip = chainedHeader ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedHeader.HashBlock;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.blocksQueue.Dispose();
        }
    }
}
