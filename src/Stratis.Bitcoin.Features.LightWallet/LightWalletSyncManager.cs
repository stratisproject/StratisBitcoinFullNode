using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet
{
    public class LightWalletSyncManager : IWalletSyncManager
    {
        /// <summary>This async loop is used to wait for the chain headers to download. The headers need to at least be downloaded up until the requested sync date.</summary>
        private IAsyncLoop syncFromDateAsyncLoop;

        /// <summary>This async loop waits for the underlying block puller to re-download blocks once a sync height has been supplied.</summary>
        private IAsyncLoop syncFromHeightAsyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private readonly IWalletManager walletManager;

        private readonly ConcurrentChain chain;

        private readonly IBlockNotification blockNotification;

        private readonly ILogger logger;

        private readonly ISignals signals;

        protected ChainedHeader walletTip;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private IDisposable sub;

        private IDisposable txSub;

        private int syncWalletHeight;
        private DateTime syncWalletDate = DateTime.MinValue;
        public ChainedHeader WalletTip => this.walletTip;

        public LightWalletSyncManager(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            ConcurrentChain chain,
            Network network,
            IBlockNotification blockNotification,
            ISignals signals,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockNotification, nameof(blockNotification));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.walletManager = walletManager;
            this.chain = chain;
            this.signals = signals;
            this.blockNotification = blockNotification;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
        }

        /// <inheritdoc />
        public void Start()
        {
            // subscribe to receiving blocks and transactions
            this.sub = this.signals.SubscribeForBlocksConnected(new BlockObserver(this));
            this.txSub = this.signals.SubscribeForTransactions(new TransactionObserver(this));

            // if there is no wallet created yet, the wallet tip is the chain tip.
            if (!this.walletManager.ContainsWallets)
            {
                this.walletTip = this.chain.Tip;
            }
            else
            {
                this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
                if (this.walletTip == null && this.chain.Height > 0)
                {
                    // the wallet tip was not found in the main chain.
                    // this can happen if the node crashes unexpectedly.
                    // to recover we need to find the first common fork
                    // with the best chain, as the wallet does not have a
                    // list of chain headers we use a BlockLocator and persist
                    // that in the wallet. the block locator will help finding
                    // a common fork and bringing the wallet back to a good
                    // state (behind the best chain)
                    ICollection<uint256> locators = this.walletManager.GetFirstWalletBlockLocator();
                    var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                    ChainedHeader fork = this.chain.FindFork(blockLocator);
                    this.walletManager.RemoveBlocks(fork);
                    this.walletManager.WalletTipHash = fork.HashBlock;
                    this.walletTip = fork;
                    this.logger.LogWarning($"Wallet tip was out of sync, wallet tip reverted back to Height = {this.walletTip.Height} hash = {this.walletTip.HashBlock}.");
                }

                // we're looking from where to start syncing the wallets.
                // we start by looking at the heights of the wallets and we start syncing from the oldest one (the smallest height).
                // if for some reason we can't find a height, we look at the creation date of the wallets and we start syncing from the earliest date.
                int? earliestWalletHeight = this.walletManager.GetEarliestWalletHeight();
                if (earliestWalletHeight == null)
                {
                    DateTimeOffset oldestWalletDate = this.walletManager.GetOldestWalletCreationTime();

                    if (oldestWalletDate > this.walletTip.Header.BlockTime)
                    {
                        oldestWalletDate = this.walletTip.Header.BlockTime;
                    }

                    this.SyncFromDate(oldestWalletDate.LocalDateTime);
                }
                else
                {
                    // If we reorged and the fork point is before the earliest wallet height start to
                    // sync from the fork point.
                    // We'll also get into this branch if the chain has been deleted but wallets are present.
                    // In this case, the wallet tip will be null so the next statement will be skipped.
                    if (this.walletTip != null && earliestWalletHeight.Value > this.walletTip.Height)
                    {
                        earliestWalletHeight = this.walletTip.Height;
                    }

                    this.SyncFromHeight(earliestWalletHeight.Value);
                }
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.syncFromHeightAsyncLoop != null)
            {
                this.syncFromHeightAsyncLoop.Dispose();
                this.syncFromHeightAsyncLoop = null;
            }

            if (this.syncFromDateAsyncLoop != null)
            {
                this.syncFromDateAsyncLoop.Dispose();
                this.syncFromDateAsyncLoop = null;
            }

            if (this.sub != null)
            {
                this.sub.Dispose();
                this.sub = null;
            }

            if (this.txSub != null)
            {
                this.txSub.Dispose();
                this.txSub = null;
            }
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            ChainedHeader newTip = this.chain.GetBlock(block.GetHash());
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
                ChainedHeader inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                if (inBestChain == null)
                {
                    // The current wallet hash was not found on the main chain.
                    // A reorg happened so bring the wallet back top the last known fork.
                    ChainedHeader fork = this.walletTip;

                    // We walk back the chained block object to find the fork.
                    while (this.chain.GetBlock(fork.HashBlock) == null)
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

                    // The wallet is falling behind we need to catch up.
                    this.logger.LogWarning("New tip '{0}' is too far in advance, put the puller back.", newTip);
                    this.blockNotification.SyncFrom(this.walletTip.HashBlock);
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

                    this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.walletTip, newTip.HashBlock);
                }
            }
            else this.logger.LogTrace("New block follows the previously known block '{0}'.", this.walletTip);

            this.walletTip = newTip;
            this.walletManager.ProcessBlock(block, newTip);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        /// <inheritdoc />
        public void SyncFromDate(DateTime date)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(date), date);

            if ((this.syncWalletDate == DateTime.MinValue) || (this.walletTip.Header.BlockTime.LocalDateTime > date))
            {
                this.logger.LogTrace("Setting new wallet sync from date to {0}; wallet's previous sync from date was {1}.", date, this.syncWalletDate);
                this.syncWalletDate = date;
            }
            else
            {
                this.logger.LogTrace("Ignoring request to sync from date {0} as the wallet is already syncing from older date {1}. Subsequent call to SyncFromHeight might amend the block hight.", date, this.walletTip.Header.BlockTime.LocalDateTime);
            }

            // Check if the task is already running and don't start another asyncloop call if we are already running one.
            if (this.syncFromDateAsyncLoop != null)
            {
                this.logger.LogTrace("(-)[SYNCFROMDATEASYNCLOOP_NOT_NULL]");
                return;
            }

            // Before we start syncing we need to make sure that the chain is at a certain level.
            // If the chain is behind the date from which we want to sync, we wait for it to catch up, and then we start syncing.
            // If the chain is already past the date we want to sync from, we don't wait, even though the chain might not be fully downloaded.
            if (this.chain.Tip.Header.BlockTime.LocalDateTime < this.syncWalletDate)
            {
                this.logger.LogTrace("The chain tip's date ({0}) is behind the date from which we want to sync ({1}). Waiting for the chain to catch up.", this.chain.Tip.Header.BlockTime.LocalDateTime, this.syncWalletDate);

                this.syncFromDateAsyncLoop = this.asyncLoopFactory.RunUntil("LightWalletSyncManager.SyncFromDate", this.nodeLifetime.ApplicationStopping,
                    () => this.chain.Tip.Header.BlockTime.LocalDateTime >= this.syncWalletDate,
                    () =>
                    {
                        int blockHeightAtDate = this.chain.GetHeightAtTime(this.syncWalletDate);
                        this.logger.LogTrace("Start syncing from {0} (block: {1}).", this.syncWalletDate, blockHeightAtDate);
                        SyncFromHeight(blockHeightAtDate);

                        // Set the asyncloop to null so that it can be used again.
                        this.syncFromDateAsyncLoop = null;
                    },
                    (ex) =>
                    {
                        // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and sync from the current height.
                        this.logger.LogError("Exception occurred while waiting for chain to download: {0}.", ex.Message);
                        this.StartSync(this.chain.Tip.Height);

                        // Set the asyncloop to null so that it could be used again.
                        this.syncFromDateAsyncLoop = null;
                    },
                    TimeSpans.FiveSeconds);
            }
            else
            {
                int blockHeightAtDate = this.chain.GetHeightAtTime(this.syncWalletDate);
                this.logger.LogTrace("Start syncing from {0} (block: {1}).", this.syncWalletDate, blockHeightAtDate);
                SyncFromHeight(blockHeightAtDate);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void SyncFromHeight(int height)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(height), height);

            if (height < 0)
            {
                throw new WalletException($"Invalid block height {height}. The height must be zero or higher.");
            }

            // Set the syncWalletHeight to the height passed in parameter if one of the conditions is true:
            // The syncWalletHeight is 0 meaning that the it is the first ever call to current method
            // The height passed as parameter is equal or lower than current wallet tip
            if ((this.syncWalletHeight == 0) || (height <= this.walletTip.Height))
            {
                // This will update the condition within the async loop below.
                this.logger.LogTrace("Setting new wallet sync height to {0}; wallet's previous sync height was {1}.", height, this.syncWalletHeight);
                this.syncWalletHeight = height;
            }
            else
            {
                this.logger.LogTrace("Ignoring request to sync from block height {0} as the wallet is already syncing from lower block height {1}.", height, this.walletTip.Height);
                this.logger.LogTrace("(-)[ALREADY_SYNCING_LOWER]");
                return;
            }

            // Check if the task is already running and don't start another asyncloop call if we are already running one.
            if (this.syncFromHeightAsyncLoop != null)
            {
                this.logger.LogTrace("(-)[ALREADY_RUNNING]");
                return;
            }

            // Before we start syncing we need to make sure that the chain is at a certain level.
            // If the chain is behind the height from which we want to sync, we wait for it to catch up, and then we start syncing.
            // If the chain is already past the height we want to sync from, we don't wait, even though the chain might  not be fully downloaded.
            if (this.chain.Tip.Height < this.syncWalletHeight)
            {
                this.logger.LogTrace("The chain tip's height ({0}) is lower than the tip height from which we want to sync ({1}). Waiting for the chain to catch up.", this.chain.Tip.Height, this.syncWalletHeight);

                this.syncFromHeightAsyncLoop = this.asyncLoopFactory.RunUntil("LightWalletSyncManager.SyncFromHeight", this.nodeLifetime.ApplicationStopping,
                    () => this.chain.Tip.Height >= this.syncWalletHeight,
                    () =>
                    {
                        this.logger.LogTrace("Start syncing from height {0}.", this.syncWalletHeight);
                        this.StartSync(this.syncWalletHeight);

                        // Set the asyncloop to null so that it can be used again.
                        this.syncFromHeightAsyncLoop = null;
                    },
                    (ex) =>
                    {
                        // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and sync from the current height.
                        this.logger.LogError("Exception occurred while waiting for chain to download: {0}.", ex.Message);
                        this.StartSync(this.chain.Tip.Height);

                        // Set the asyncloop to null so that it can be used again.
                        this.syncFromHeightAsyncLoop = null;
                    },
                    TimeSpans.FiveSeconds);
            }
            else
            {
                this.logger.LogTrace("Start syncing from height {0}.", this.syncWalletHeight);
                this.StartSync(this.syncWalletHeight);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts pulling blocks from the required height.
        /// </summary>
        /// <param name="height">The height from which to get blocks.</param>
        private void StartSync(int height)
        {
            // TODO add support for the case where there is a reorg, like in the initialize method
            ChainedHeader chainedHeader = this.chain.GetBlock(height);
            this.walletTip = chainedHeader ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedHeader.HashBlock;
            this.blockNotification.SyncFrom(chainedHeader.HashBlock);
        }
    }
}