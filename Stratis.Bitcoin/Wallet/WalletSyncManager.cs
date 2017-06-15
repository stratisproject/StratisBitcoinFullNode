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

        protected ChainedBlock lastReceivedBlock;

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
            this.logger.LogInformation($"WalletSyncManager initialized. wallet at block {this.walletManager.LastBlockHeight()}.");

            this.lastReceivedBlock = this.chain.GetBlock(this.walletManager.LastReceivedBlock);
            if (this.lastReceivedBlock == null)
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
            if (block.Header.HashPrevBlock != this.lastReceivedBlock.HashBlock)
            {
                // if previous block does not match there might have 
                // been a reorg, check if the wallet is still on the main chain
                var current = this.chain.GetBlock(this.lastReceivedBlock.HashBlock);
                if (current == null)
                {
                    // the current wallet hash was not found on the main chain
                    // a reorg happenend so bring the wallet back top the last known fork

                    var fork = this.lastReceivedBlock;

                    // we walk back the chained block object to find the fork
                    while (this.chain.GetBlock(fork.HashBlock) == null)
                        fork = fork.Previous;

                    Guard.Assert(fork.HashBlock == block.Header.HashPrevBlock);
                    this.walletManager.RemoveBlocks(fork);
                }
                else
                {
                    var chainedBlock = this.chain.GetBlock(block.GetHash());
                    if (chainedBlock.Height > this.lastReceivedBlock.Height)
                        // the wallet is falling behind we need to catch up
                        throw new NotImplementedException();
                }
            }

            this.lastReceivedBlock = this.chain.GetBlock(block.GetHash());
            this.walletManager.ProcessBlock(block, this.lastReceivedBlock);
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
            this.lastReceivedBlock = chainedBlock;
            this.walletManager.LastReceivedBlock = chainedBlock.HashBlock;
        }
    }
}
