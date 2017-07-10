using Moq;
using NBitcoin;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.Wallet.Notifications;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.Wallet.Notifications
{
    public class TransactionObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            TransactionObserver observer = new TransactionObserver(walletSyncManager.Object);
            Transaction transaction = new Transaction();

            observer.OnNext(transaction);

            walletSyncManager.Verify(w => w.ProcessTransaction(transaction), Times.Exactly(1));
        }
    }
}
