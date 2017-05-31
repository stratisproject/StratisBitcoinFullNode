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
    public class Tracker : ITracker
    {
        private readonly WalletManager walletManager;
        private readonly ConcurrentChain chain;
        private readonly Signals signals;
        private readonly CoinType coinType;
        private readonly ILogger logger;


		public Tracker(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain, Signals signals, 
			Network network)
        {
            this.walletManager = walletManager as WalletManager;
            this.chain = chain;
            this.signals = signals;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize()
        {

	        // subscribe to receiving blocks and transactions
			IDisposable sub = new BlockSubscriber(this.signals.Blocks, new BlockObserver(this.chain, this.walletManager)).Subscribe();
	        IDisposable txSub = new TransactionSubscriber(this.signals.Transactions, new TransactionObserver(this.walletManager)).Subscribe();

            // start syncing blocks
            var bestHeightForSyncing = this.FindBestHeightForSyncing();
            this.logger.LogInformation($"Tracker initialized. wallet at block {bestHeightForSyncing}.");
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
