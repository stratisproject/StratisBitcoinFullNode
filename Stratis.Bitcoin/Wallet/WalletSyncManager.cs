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

namespace Stratis.Bitcoin.Wallet
{
    public class WalletSyncManager : IWalletSyncManager
    {
        protected readonly WalletManager walletManager;
        protected readonly ConcurrentChain chain;
        protected readonly CoinType coinType;
        protected readonly ILogger logger;


        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain, Network network)
        {
            this.walletManager = walletManager as WalletManager;
            this.chain = chain;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public virtual async Task Initialize()
        {
            // start syncing blocks
            var bestHeightForSyncing = this.FindBestHeightForSyncing();
            this.logger.LogInformation($"WalletSyncManager initialized. wallet at block {bestHeightForSyncing}.");


            // try to detect if a reorg happened when offline.
            var current = this.chain.GetBlock(this.walletManager.LastReceivedBlockHash());
            if (current == null)
            {
                // the current wallet hash was not found on the main chain
                // a reorg happenend so bring the wallet back top the last known fork

                var blockstoremove = new List<uint256>();
                var fork = this.walletManager.LastReceivedBlock;

                // we walk back the chained block object to find the fork
                while (this.chain.GetBlock(fork.HashBlock) == null)
                {
                    blockstoremove.Add(fork.HashBlock);
                    fork = fork.Previous;
                }

                this.walletManager.RemoveBlocks(fork);
            }

            await Task.CompletedTask;
        }

        public void ProcessBlock(Block block)
        {
            if (block.Header.HashPrevBlock != this.walletManager.LastReceivedBlock.HashBlock)
            {
                // if previous block does not match there might have 
                // been a reorg, check if we still on the main chain
                var current = this.chain.GetBlock(this.walletManager.LastReceivedBlock.HashBlock);
                if (current == null)
                {
                    // the current wallet hash was not found on the main chain
                    // a reorg happenend so bring the wallet back top the last known fork

                    var blockstoremove = new List<uint256>();
                    var fork = this.walletManager.LastReceivedBlock;

                    // we walk back the chained block object to find the fork
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                    {
                        blockstoremove.Add(fork.HashBlock);
                        fork = fork.Previous;
                    }

                    this.walletManager.RemoveBlocks(fork);
                }
                else if (current.Height > this.walletManager.LastReceivedBlock.Height)
                {
                    // the wallet is falling behind we need to catch up
                    throw new NotImplementedException();
                }
            }

            var chainedBlock = this.chain.GetBlock(block.GetHash());
            this.walletManager.ProcessBlock(block, chainedBlock);
        }

        public void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        public virtual void SyncFrom(DateTime date)
        {
            // TODO: this will enable resyncing the wallet from an earlier block
            // this means the syncer will need to find the blocks 
            // either form the block store or download them in case of a pruned node
        }

        public virtual void SyncFrom(int height)
        {
        }

        private int FindBestHeightForSyncing()
        {
            // if there are no wallets, get blocks from now
            if (!this.walletManager.Wallets.Any())
            {
                return this.chain.Tip.Height;
            }

            // sync the accounts with new blocks, starting from the most out of date
            int? syncFromHeight = this.walletManager.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
            if (syncFromHeight == null)
            {
                return this.chain.Tip.Height;
            }

            return Math.Min(syncFromHeight.Value, this.chain.Tip.Height);
        }
    }
}
