using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet
{
    public class LightWalletSyncManager : IWalletSyncManager
    {
        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncProvider asyncProvider;

        private readonly IWalletManager walletManager;

        private readonly ChainIndexer chainIndexer;

        private readonly IBlockNotification blockNotification;

        private readonly ILogger logger;

        private readonly ISignals signals;

        protected ChainedHeader walletTip;

        private SubscriptionToken transactionAddedSubscription;
        private SubscriptionToken transactionRemovedSubscription;
        private SubscriptionToken blockConnectedSubscription;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        public ChainedHeader WalletTip => this.walletTip;

        private readonly IConsensusManager consensusManager;

        public LightWalletSyncManager(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            ChainIndexer chainIndexer,
            Network network,
            IBlockNotification blockNotification,
            ISignals signals,
            INodeLifetime nodeLifetime,
            IAsyncProvider asyncProvider,
            IConsensusManager consensusManager)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockNotification, nameof(blockNotification));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(consensusManager, nameof(consensusManager));

            this.walletManager = walletManager;
            this.chainIndexer = chainIndexer;
            this.signals = signals;
            this.blockNotification = blockNotification;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
            this.consensusManager = consensusManager;
        }

        /// <inheritdoc />
        public void Start()
        {
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);

            // if there is no wallet created yet, the wallet tip is the chain tip.
            if (!this.walletManager.ContainsWallets)
            {
                this.walletTip = this.chainIndexer.Tip;
            }
            else
            {
                this.walletTip = this.chainIndexer.GetHeader(this.walletManager.WalletTipHash);
            }

            this.transactionAddedSubscription = this.signals.Subscribe<TransactionAddedToMemoryPool>(this.OnTransactionAdded);
            this.transactionRemovedSubscription = this.signals.Subscribe<TransactionRemovedFromMemoryPool>(this.OnTransactionRemoved);
        }

        private void OnTransactionAdded(TransactionAddedToMemoryPool transactionAdded)
        {
            this.walletManager.ProcessTransaction(transactionAdded.AddedTransaction);
        }

        private void OnTransactionRemoved(TransactionRemovedFromMemoryPool transactionRemoved)
        {
            this.walletManager.RemoveUnconfirmedTransaction(transactionRemoved.RemovedTransaction);
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            this.ProcessBlock(blockConnected.ConnectedBlock.Block);
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.asyncLoop != null)
            {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }

            this.signals.Unsubscribe(this.blockConnectedSubscription);
            this.signals.Unsubscribe(this.transactionAddedSubscription);
            this.signals.Unsubscribe(this.transactionRemovedSubscription);
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            ChainedHeader newTip = this.chainIndexer.GetHeader(block.GetHash());
            if (newTip == null)
            {
                this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                return;
            }

            // If the new block's previous hash is the same as the
            // wallet hash then just pass the block to the manager.
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

                    this.logger.LogDebug("Wallet tip set to '{0}'.", this.walletTip);
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

                    this.logger.LogDebug("Wallet tip '{0}' is behind the new tip '{1}'.", this.walletTip, newTip);

                    // The wallet is falling behind we need to catch up.
                    this.logger.LogWarning("New tip '{0}' is too far in advance.", newTip);

                    CancellationToken token = this.nodeLifetime.ApplicationStopping;

                    ChainedHeader next = this.walletTip;
                    while (next != newTip)
                    {
                        // While the wallet is catching up the entire node will wait.
                        // If a wallet is recovered to a date in the past. Consensus will stop until the wallet is up to date.

                        // TODO: This code should be replaced with a different approach
                        // Similar to BlockStore the wallet should be standalone and not depend on consensus.
                        // The block should be put in a queue and pushed to the wallet in an async way.
                        // If the wallet is behind it will just read blocks from store (or download in case of a pruned node).

                        token.ThrowIfCancellationRequested();

                        next = newTip.GetAncestor(next.Height + 1);
                        ChainedHeaderBlock nextblock = null;
                        int index = 0;
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();

                            nextblock = this.consensusManager.GetBlockData(next.HashBlock);
                            if (nextblock != null && nextblock.Block != null)
                                break;

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

                        this.walletTip = next;
                        this.walletManager.ProcessBlock(nextblock.Block, next);
                    }

                    return;
                }
                else
                {
                    ChainedHeader findTip = this.walletTip.FindAncestorOrSelf(newTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_BEHIND_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogDebug("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.walletTip, newTip.HashBlock);
                }
            }
            else
                this.logger.LogDebug("New block follows the previously known block '{0}'.", this.walletTip);

            this.walletTip = newTip;
            this.walletManager.ProcessBlock(block, newTip);
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        /// <inheritdoc />
        public void SyncFromDate(DateTime date, string walletName = null)
        {
            // Before we start syncing we need to make sure that the chain is at a certain level.
            // If the chain is behind the date from which we want to sync, we wait for it to catch up, and then we start syncing.
            // If the chain is already past the date we want to sync from, we don't wait, even though the chain might not be fully downloaded.
            if (this.chainIndexer.Tip.Header.BlockTime.LocalDateTime < date)
            {
                this.logger.LogDebug("The chain tip's date ({0}) is behind the date from which we want to sync ({1}). Waiting for the chain to catch up.", this.chainIndexer.Tip.Header.BlockTime.LocalDateTime, date);

                this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoopUntil("LightWalletSyncManager.SyncFromDate", this.nodeLifetime.ApplicationStopping,
                    () => this.chainIndexer.Tip.Header.BlockTime.LocalDateTime >= date,
                    () =>
                    {
                        this.logger.LogDebug("Start syncing from {0}.", date);
                        this.StartSync(this.chainIndexer.GetHeightAtTime(date));
                    },
                    (ex) =>
                    {
                        // in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                        // sync from the current height.
                        this.logger.LogError("Exception occurred while waiting for chain to download: {0}.", ex.Message);
                        this.StartSync(this.chainIndexer.Tip.Height);
                    },
                    TimeSpans.FiveSeconds);
            }
            else
            {
                this.logger.LogDebug("Start syncing from {0}", date);
                this.StartSync(this.chainIndexer.GetHeightAtTime(date));
            }
        }

        /// <inheritdoc />
        public void SyncFromHeight(int height, string walletName = null)
        {
            if (height < 0)
            {
                throw new WalletException($"Invalid block height {height}. The height must be zero or higher.");
            }

            // Before we start syncing we need to make sure that the chain is at a certain level.
            // If the chain is behind the height from which we want to sync, we wait for it to catch up, and then we start syncing.
            // If the chain is already past the height we want to sync from, we don't wait, even though the chain might  not be fully downloaded.
            if (this.chainIndexer.Tip.Height < height)
            {
                this.logger.LogDebug("The chain tip's height ({0}) is lower than the tip height from which we want to sync ({1}). Waiting for the chain to catch up.", this.chainIndexer.Tip.Height, height);

                this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoopUntil("LightWalletSyncManager.SyncFromHeight", this.nodeLifetime.ApplicationStopping,
                    () => this.chainIndexer.Tip.Height >= height,
                    () =>
                    {
                        this.logger.LogDebug("Start syncing from height {0}.", height);
                        this.StartSync(height);
                    },
                    (ex) =>
                    {
                        // in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                        // sync from the current height.
                        this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");
                        this.StartSync(this.chainIndexer.Tip.Height);
                    },
                    TimeSpans.FiveSeconds);
            }
            else
            {
                this.logger.LogDebug("Start syncing from height {0}.", height);
                this.StartSync(height);
            }
        }

        /// <summary>
        /// Starts pulling blocks from the required height.
        /// </summary>
        /// <param name="height">The height from which to get blocks.</param>
        private void StartSync(int height)
        {
            // TODO add support for the case where there is a reorg, like in the initialize method
            ChainedHeader chainedHeader = this.chainIndexer.GetHeader(height);
            this.walletTip = this.walletManager.WalletCommonTip(chainedHeader ?? throw new WalletException("Invalid block height"));
            this.blockNotification.SyncFrom(chainedHeader.HashBlock);
        }
    }
}
