using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
    /// </summary>
    public class TransactionObserver : SignalObserver<Transaction>
    {
        private readonly IFederationWalletSyncManager walletSyncManager;

        public TransactionObserver(IFederationWalletSyncManager walletSyncManager)
        {
            this.walletSyncManager = walletSyncManager;
        }

        /// <summary>
        /// Manages what happens when a new transaction is received.
        /// </summary>
        /// <param name="transaction">The new transaction</param>
        protected override void OnNextCore(Transaction transaction)
        {
            this.walletSyncManager.ProcessTransaction(transaction);
        }
    }
}
