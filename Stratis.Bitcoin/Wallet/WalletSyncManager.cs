using System;
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
           
            await Task.CompletedTask;
        }

        public void ProcessBlock(Block block)
        {
            this.walletManager.ProcessBlock(block);
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
