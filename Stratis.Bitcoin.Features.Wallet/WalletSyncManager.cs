using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager
    {
        protected readonly IWalletManager walletManager;
        protected readonly ConcurrentChain chain;
        protected readonly CoinType coinType;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

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
            this.logger.LogTrace("()");
            
            // When a node is pruned it impossible to catch up 
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be 
            // able to download blocks from peers to catch up.
            if (this.storeSettings.Prune)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.", this.walletManager.LastBlockHeight());

            this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
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
                ICollection<uint256> locators = this.walletManager.GetFirstWalletBlockLocator();
                BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedBlock fork = this.chain.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                this.walletManager.WalletTipHash = fork.HashBlock;
                this.walletTip = fork;
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.logger.LogTrace("()");
            this.logger.LogTrace("(-)");
        }

        public virtual void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            // If the new block's previous hash is the same as the 
            // wallet hash then just pass the block to the manager. 
            var newTip = this.chain.GetBlock(block.GetHash());

            // new block is not in the main chain nothing more to do right now. 
            if (newTip == null)
                return; // reorg

            if (block.Header.HashPrevBlock != this.walletTip.HashBlock)
            {
                // If previous block does not match there might have 
                // been a reorg, check if the wallet is still on the main chain.
                ChainedBlock inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                if (inBestChain == null)
                {
                    // The current wallet hash was not found on the main chain.
                    // A reorg happened so bring the wallet back top the last known fork.

                    ChainedBlock fork = this.walletTip;

                    // We walk back the chained block object to find the fork.
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                        fork = fork.Previous;

                    this.logger.LogTrace("Reorganization detected, going back to '{0}/{1}'.", fork.HashBlock, fork.Height);

                    this.walletManager.RemoveBlocks(fork);

                    // Update temporary wallet tip to be able to catch up again.
                    this.walletTip = fork;
                    this.logger.LogTrace("Wallet tip set to '{0}/{1}'.", walletTip.HashBlock, walletTip.Height);
                }

                // The new tip can be ahead or behind the wallet
                // If the new tip is ahead we try to bring the wallet up to the new tip.
                // If the new tip is behind we just check the wallet and the tip ad in the same chain.
            
                if (newTip.Height > this.walletTip.Height)
                {
                    // check that the new tip is in the chain of the wallet tip
                    var findTip = newTip.FindAncestorOrSelf(this.walletTip.HashBlock);
                    Guard.Assert(findTip == this.walletTip); // this should never happen

                    var token = this.nodeLifetime.ApplicationStopping;

                    // The wallet is falling behind we need to catch up.
                    ChainedBlock next = this.walletTip;
                    while (next != newTip)
                    {
                        token.ThrowIfCancellationRequested();

                        // While the wallet is catching up the entire node will wait
                        // if a wallet recovers to a date in the past. Consensus 
                        // will stop till the wallet is up to date.

                        // TODO: This code should be replaced with a different approach
                        // similar to BlockStore the wallet should be standalone and not depend on consensus
                        // the block should be put in a queue and pushed to the wallet in an async way
                        // if the wallet is behind it will just read blocks from store (or download in case of a pruned node).
                        next = newTip.GetAncestor(next.Height + 1);
                        Block nextblock = null;
                        int index = 0;
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();

                            nextblock = this.blockStoreCache.GetBlockAsync(next.HashBlock).GetAwaiter().GetResult();
                            if (nextblock == null)
                            {
                                // The idea in this abandoning of the loop is to release consensus to push the block.
                                // That will make the block available in the next push from conensus.
                                index++;
                                if (index > 10)
                                    return;

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
                    // check that the new tip is in the chain of the wallet tip
                    var findTip = this.walletTip.FindAncestorOrSelf(newTip.HashBlock);
                    Guard.Assert(findTip == newTip); // this should never happen
                }
                else this.logger.LogTrace("New block's height {0} is not above wallet's tip height {1}.", incomingBlock.Height, this.walletTip.Height);
            }
            else this.logger.LogTrace("New block follows the previously known block '{0}'.", this.walletTip.HashBlock);

            this.walletTip = newTip;
            this.walletManager.ProcessBlock(block, this.walletTip);

            this.logger.LogTrace("(-)");
        }

        public virtual void ProcessTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            this.logger.LogTrace("({0}:'{1}')", nameof(transaction), transaction.GetHash());

            this.walletManager.ProcessTransaction(transaction);

            this.logger.LogTrace("(-)");
        }

        public virtual void SyncFromDate(DateTime date)
        {
            this.logger.LogTrace("({0}:'{1::yyyy-MM-dd HH:mm:ss}')", nameof(date), date);

            int blockSyncStart = this.chain.GetHeightAtTime(date);
            this.SyncFromHeight(blockSyncStart);

            this.logger.LogTrace("(-)");
        }

        public virtual void SyncFromHeight(int height)
        {
            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            ChainedBlock chainedBlock = this.chain.GetBlock(height);
            this.walletTip = chainedBlock ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedBlock.HashBlock;

            this.logger.LogTrace("(-)");
        }
    }
}