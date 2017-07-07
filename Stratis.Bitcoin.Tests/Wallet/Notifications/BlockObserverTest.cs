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
    public class BlockObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            BlockObserver observer = new BlockObserver(walletSyncManager.Object);
            Block block = new Block();

            observer.OnNext(block);

            walletSyncManager.Verify(w => w.ProcessBlock(block), Times.Exactly(1));
        }
    }
}
