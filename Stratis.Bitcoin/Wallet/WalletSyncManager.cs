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

        private ChainedBlock lastReceivedBlock;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain, Network network)
        {
            this.walletManager = walletManager as WalletManager;
            this.chain = chain;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public virtual Task Initialize()
        {
            // initialize the wallet
            this.walletManager.Initialize();

            this.logger.LogInformation($"WalletSyncManager initialized. wallet at block {this.walletManager.LastBlockHeight()}.");

            if (!this.walletManager.Wallets.Any())
                return Task.CompletedTask;

            // try to detect if a reorg happened when offline.
            this.lastReceivedBlock = this.chain.GetBlock(this.walletManager.LastReceivedBlock);
            if (this.lastReceivedBlock == null)
            {
                // if a fork happeend when the wallet was offline
                // there is no way to know the block that forked as 
                // the wallet does not persist the chain of headers.
                // to recover from a reorg we use the blocklocator
                // the block locator keeps an incremental list of hash
                // headers this will help determine the last chain the
                // wallet was on and allow to find the fork.

                var blockstoremove = new List<uint256>();
                var locators = this.walletManager.Wallets.First().BlockLocator;
                BlockLocator blockLocator = new BlockLocator { Blocks = locators.ToList() };
                var fork = this.chain.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
            }

            return Task.CompletedTask;
        }

        public void ProcessBlock(Block block)
        {
            // if the new block previous hash is the same as the 
            // wallet hash then just pass the block to the manager 
            if (block.Header.HashPrevBlock != this.walletManager.LastReceivedBlock)
            {
                // if previous block does not match there might have 
                // been a reorg, check if the wallet is still on the main chain
                var current = this.chain.GetBlock(this.lastReceivedBlock.HashBlock);
                if (current == null)
                {
                    // the current wallet hash was not found on the main chain
                    // a reorg happenend so bring the wallet back top the last known fork

                    var blockstoremove = new List<uint256>();
                    var fork = this.lastReceivedBlock;

                    // we walk back the chained block object to find the fork
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                    {
                        blockstoremove.Add(fork.HashBlock);
                        fork = fork.Previous;
                    }

                    this.walletManager.RemoveBlocks(fork);
                }
                else if (current.Height > this.lastReceivedBlock.Height)
                {
                    // the wallet is falling behind we need to catch up
                    throw new NotImplementedException();
                }
            }

            this.lastReceivedBlock = this.chain.GetBlock(block.GetHash());
            this.walletManager.ProcessBlock(block, this.lastReceivedBlock);
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
    }
}
