using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Wallet.Notifications;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Notifications;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Wallet
{
    public class WalletSyncManager : IWalletSyncManager
    {
        protected readonly WalletManager walletManager;
        protected readonly ConcurrentChain chain;
        protected readonly CoinType coinType;
        protected readonly ILogger logger;
        private readonly BlockStoreCache blockStoreCache;
        private readonly NodeSettings nodeSettings;

        protected ChainedBlock walletTip;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain, 
            Network network, BlockStoreCache blockStoreCache, NodeSettings nodeSettings)
        {
            this.walletManager = walletManager as WalletManager;
            this.chain = chain;
            this.blockStoreCache = blockStoreCache;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public virtual Task Initialize()
        {
            // when a node is pruned it imposible to catch up 
            // if the wallet falls behind the block puller.
            // to support pruning the wallet will need to be 
            // able to download blocks from peers to catch up.
            if (this.nodeSettings.Store.Prune)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation($"WalletSyncManager initialized. wallet at block {this.walletManager.LastBlockHeight()}.");

            this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
            if (this.walletTip == null)
                throw new WalletException("Wallet tip was not found in the best chain, rescan the wallet");

            // offline reorg is extreamly reare it will 
            // only happen if the node crashes during a reorg

            //var blockstoremove = new List<uint256>();
            //var locators = this.walletManager.Wallets.First().BlockLocator;
            //BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
            //var fork = this.chain.FindFork(blockLocator);
            //this.walletManager.RemoveBlocks(fork);

            return Task.CompletedTask;
        }

        public virtual void ProcessBlock(Block block)
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
                }
                else
                {
                    ChainedBlock incomingBlock = this.chain.GetBlock(block.GetHash());
                    if (incomingBlock.Height > this.walletTip.Height)
                    {
                        // the wallet is falling behind we need to catch up
                        var next = this.walletTip;
                        while(next != incomingBlock)
                        {
                            // while the wallet is catching up the entire node will hult
                            // if a wallet recoveres to a date in the past consensus 
                            // will stop til the wallet is up to date.

                            next = this.chain.GetBlock(next.Height +1);
                            var nextblock = this.blockStoreCache.GetBlockAsync(next.HashBlock).GetAwaiter().GetResult();
                            if(nextblock == null)
                                return; // temporary to allow wallet ot recover when store is behind
                            this.walletManager.ProcessBlock(nextblock, next);
                        }
                    }
                }
            }

            this.walletTip = this.chain.GetBlock(block.GetHash());
            this.walletManager.ProcessBlock(block, this.walletTip);
        }

        public virtual void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        public virtual void SyncFrom(DateTime date)
        {
            int blockSyncStart = this.chain.GetHeightAtTime(date);
            this.SyncFrom(blockSyncStart);
        }

        public virtual void SyncFrom(int height)
        {
            var chainedBlock = this.chain.GetBlock(height);
            if(chainedBlock == null)
                throw  new WalletException("Invalid block height");
            this.walletTip = chainedBlock;
            this.walletManager.WalletTipHash = chainedBlock.HashBlock;
        }
    }
}
