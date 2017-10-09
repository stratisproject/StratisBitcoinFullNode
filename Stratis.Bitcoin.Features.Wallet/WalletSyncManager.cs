using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using System;
using System.Linq;
using System.Threading;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager
    {
        protected readonly IWalletManager walletManager;
        protected readonly ConcurrentChain chain;
        protected readonly CoinType coinType;
        protected readonly ILogger logger;
        private readonly IBlockStoreCache blockStoreCache;
        private readonly StoreSettings storeSettings;
        private readonly INodeLifetime nodeLifetime;

        protected ChainedBlock walletTip;

        public ChainedBlock WalletTip => this.walletTip;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
            Network network, IBlockStoreCache blockStoreCache, StoreSettings storeSettings, INodeLifetime nodeLifetime)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.walletManager = walletManager;
            this.chain = chain;
            this.blockStoreCache = blockStoreCache;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.storeSettings = storeSettings;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Start()
        {
            // when a node is pruned it imposible to catch up 
            // if the wallet falls behind the block puller.
            // to support pruning the wallet will need to be 
            // able to download blocks from peers to catch up.
            if (this.storeSettings.Prune)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation($"WalletSyncManager initialized. wallet at block {this.walletManager.LastBlockHeight()}.");

            this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
            if (this.walletTip == null)
            {
                // the wallet tip was not found in the main chain.
                // this can happen if the node crashes unexpectedly.
                // to recover we need to find the first common fork 
                // with the best chain, as the wallet does not have a  
                // list of chain headers we use a BlockLocator and persist 
                // that in the wallet. the block locator will help finding 
                // a common fork and bringing the wallet back to a good 
                // state (behind the best chain)
                var locators = this.walletManager.GetFirstWalletBlockLocator();
                BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
                var fork = this.chain.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                this.walletManager.WalletTipHash = fork.HashBlock;
                this.walletTip = fork;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
        }

        public virtual void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            // If the new block previous hash is the same as the 
            // wallet hash then just pass the block to the manager. 
            var nextTip = this.chain.GetBlock(block.GetHash());
            if (nextTip == null || block.Header.HashPrevBlock != this.walletTip.HashBlock)
            {
                // If previous block does not match there might have 
                // been a reorg, check if the wallet is still on the main chain.
                ChainedBlock inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                if (inBestChain == null)
                {
                    // The current wallet hash was not found on the main chain.
                    // A reorg happenend so bring the wallet back top the last known fork.

                    var fork = this.walletTip;

                    // We walk back the chained block object to find the fork.
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                        fork = fork.Previous;

                    this.walletManager.RemoveBlocks(fork);

                    // update the wallet tip to be able to catch up again.
                    this.walletTip = fork;
                }

                if (nextTip == null)
                    return; // reorg

                ChainedBlock incomingBlock = nextTip;
                if (incomingBlock.Height > this.walletTip.Height)
                {
                    var token = this.nodeLifetime.ApplicationStopping;

                    // The wallet is falling behind we need to catch up.
                    var next = this.walletTip;
                    while (next != incomingBlock)
                    {
                        token.ThrowIfCancellationRequested();

                        // While the wallet is catching up the entire node will wait
                        // if a wallet recovers to a date in the past consensus 
                        // will stop till the wallet is up to date.

                        // TODO: This code should be replaced with a different approach
                        // similar to BlockStore the wallet should be standalone and not depend on consensus
                        // the block should be put in a queue and pushed to the wallet in an async way
                        // if the wallet is behind it will just read blocks from store (or download in case of a pruned node).
                        next = incomingBlock.GetAncestor(next.Height + 1);
                        Block nextblock = null;
                        var index = 0;
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();

                            nextblock = this.blockStoreCache.GetBlockAsync(next.HashBlock).GetAwaiter().GetResult();
                            if (nextblock == null)
                            {
                                // The idea in this abandoning of the loop is to release consensus to push the block
                                // That will make the block available in the next push from conensus.
                                index++;
                                if (index > 10)
                                    return;

                                // Really ugly hack to let store catch up
                                // this will block the entire consensus pulling.
                                this.logger.LogWarning("Wallet is behind the best chain and the next block is not found in store");
                                Thread.Sleep(100);
                                continue;
                            }

                            break;
                        }

                        this.walletTip = next;
                        this.walletManager.ProcessBlock(nextblock, next);
                    }
                }
            }

            this.walletTip = nextTip;
            this.walletManager.ProcessBlock(block, this.walletTip);
        }

        public virtual void ProcessTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            this.walletManager.ProcessTransaction(transaction);
        }

        public virtual void SyncFromDate(DateTime date)
        {
            int blockSyncStart = this.chain.GetHeightAtTime(date);
            this.SyncFromHeight(blockSyncStart);
        }

        public virtual void SyncFromHeight(int height)
        {
            var chainedBlock = this.chain.GetBlock(height);
            this.walletTip = chainedBlock ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedBlock.HashBlock;
        }
    }
}