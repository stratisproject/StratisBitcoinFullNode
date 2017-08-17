using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Notifications;

namespace Stratis.Bitcoin.Features.LightWallet
{
    public class LightWalletSyncManager : IWalletSyncManager
    {
        private readonly WalletManager walletManager;
        private readonly ConcurrentChain chain;
        private readonly BlockNotification blockNotification;
        private readonly CoinType coinType;
        private readonly ILogger logger;
        private readonly Signals.Signals signals;
        private ChainedBlock walletTip;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncLoopFactory asyncLoopFactory;

        public ChainedBlock WalletTip => this.walletTip;

        public LightWalletSyncManager(
            ILoggerFactory loggerFactory, 
            IWalletManager walletManager, 
            ConcurrentChain chain, 
            Network network,
            BlockNotification blockNotification, 
            Signals.Signals signals, 
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory)
        {
            this.walletManager = walletManager as WalletManager;
            this.chain = chain;
            this.signals = signals;
            this.blockNotification = blockNotification;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
        }

        /// <inheritdoc />
        public Task Initialize()
        {
            // subscribe to receiving blocks and transactions
            IDisposable sub = this.signals.SubscribeForBlocks(new BlockObserver(this));
            IDisposable txSub = this.signals.SubscribeForTransactions(new TransactionObserver(this));

            // if there is no wallet created yet, the wallet tip is the chain tip.
            if (!this.walletManager.Wallets.Any())
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
                    var locators = this.walletManager.Wallets.First().BlockLocator;
                    BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
                    var fork = this.chain.FindFork(blockLocator);
                    this.walletManager.RemoveBlocks(fork);
                    this.walletManager.WalletTipHash = fork.HashBlock;
                    this.walletTip = fork;
                    this.logger.LogWarning($"Wallet tip was out of sync, wallet tip reverted back to Height = {this.walletTip.Height} hash = {this.walletTip.HashBlock}.");
                }

                // we're looking from where to start syncing the wallets.
                // we start by looking at the heights of the wallets and we start syncing from the oldest one (the smallest height).
                // if for some reason we can't find a height, we look at the creation date of the wallets and we start syncing from the earliest date.
                int? earliestWalletHeight = this.walletManager.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
                if (earliestWalletHeight == null)
                {
                    DateTimeOffset oldestWalletDate = this.walletManager.Wallets.Min(w => w.CreationTime);
                    this.SyncFrom(oldestWalletDate.LocalDateTime);
                }
                else
                {
                    this.SyncFrom(earliestWalletHeight.Value);
                }
            }
            return Task.CompletedTask;
        }
        
        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            // if the new block previous hash is the same as the 
            // wallet hash then just pass the block to the manager 
            if (block.Header.HashPrevBlock != this.walletTip.HashBlock)
            {
                // if previous block does not match there might have 
                // been a reorg, check if the wallet is still on the main chain
                ChainedBlock inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                if (inBestChain == null)
                {
                    // the current wallet hash was not found on the main chain
                    // a reorg happenend so bring the wallet back top the last known fork

                    var fork = this.walletTip;

                    // we walk back the chained block object to find the fork
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                        fork = fork.Previous;

                    Guard.Assert(fork.HashBlock == block.Header.HashPrevBlock);
                    this.walletManager.RemoveBlocks(fork);
                    this.logger.LogWarning($"Reorg detected, wallet tip reverted back to Height = {fork.Height} hash = {fork.HashBlock}.");
                }
                else
                {
                    ChainedBlock incomingBlock = this.chain.GetBlock(block.GetHash());
                    if (incomingBlock.Height > this.walletTip.Height)
                    {
                        // the wallet is falling behind we need to catch up
                        this.logger.LogWarning($"block received with height: {inBestChain.Height} and hash: {block.Header.GetHash()} is too far in advance. put the puller back.");
                        this.blockNotification.SyncFrom(this.walletTip.HashBlock);
                        return;
                    }
                }
            }

            this.walletTip = this.chain.GetBlock(block.GetHash());
            this.walletManager.ProcessBlock(block, this.walletTip);
        }

        /// <inheritdoc />
        public void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        /// <inheritdoc />
        public void SyncFrom(DateTime date)
        {
            // before we start syncing we need to make sure that the chain is at a certain level.
            // if the chain is behind the date from which we want to sync, we wait for it to catch up, and then we start syncing.
            // if the chain is already past the date we want to sync from, we don't wait, even though the chain might not be fully downloaded.
            if (this.chain.Tip.Header.BlockTime.LocalDateTime < date)
            {
                this.asyncLoopFactory.RunUntil("WalletFeature.DownloadChain", this.nodeLifetime.ApplicationStopping,
                    () => this.chain.Tip.Header.BlockTime.LocalDateTime >= date,
                    () => this.StartSync(this.chain.GetHeightAtTime(date)),
                        (ex) =>
                        {
                            // in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and 
                            // sync from the current height.
                            this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");
                            this.StartSync(this.chain.Tip.Height);
                        },
                    TimeSpans.FiveSeconds);
            }
            else
            {               
                this.StartSync(this.chain.GetHeightAtTime(date));
            }
        }

        /// <inheritdoc />
        public void SyncFrom(int height)
        {
            // before we start syncing we need to make sure that the chain is at a certain level.
            // if the chain is behind the height from which we want to sync, we wait for it to catch up, and then we start syncing.
            // if the chain is already past the height we want to sync from, we don't wait, even though the chain might  not be fully downloaded.
            if (this.chain.Tip.Height < height)
            {
                this.asyncLoopFactory.RunUntil("WalletFeature.DownloadChain", this.nodeLifetime.ApplicationStopping,
                    () => this.chain.Tip.Height >= height,
                    () => this.StartSync(height),
                    (ex) =>
                    {
                        // in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and 
                        // sync from the current height.
                        this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");
                        this.StartSync(this.chain.Tip.Height);
                    },
                    TimeSpans.FiveSeconds);
            }
            else
            {
                this.StartSync(height);
            }
        }

        private void StartSync(int height)
        {
            // TODO add support for the case where there is a reorg, like in the initialize method
            var chainedBlock = this.chain.GetBlock(height);
            if (chainedBlock == null)
                throw new WalletException("Invalid block height");
            this.walletTip = chainedBlock;
            this.walletManager.WalletTipHash = chainedBlock.HashBlock;
            this.blockNotification.SyncFrom(chainedBlock.HashBlock);
        }
    }
}
