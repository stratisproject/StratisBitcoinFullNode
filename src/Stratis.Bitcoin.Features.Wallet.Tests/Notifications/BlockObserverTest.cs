﻿using Moq;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests.Notifications
{
    public class BlockObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            var observer = new BlockObserver(walletSyncManager.Object);
            var block = KnownNetworks.StratisMain.CreateBlock();

            observer.OnNext(block);

            walletSyncManager.Verify(w => w.ProcessBlock(block), Times.Exactly(1));
        }
    }
}