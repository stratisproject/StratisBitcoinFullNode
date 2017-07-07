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
    public class TransactionSubscriberTest
    {
        [Fact]
        public void SubscribeSubscribesObserverToSignaler()
        {
            var signaler = new Mock<ISignaler<Transaction>>();
            var observer = new TransactionObserver(new Mock<IWalletSyncManager>().Object);
            var blockSubscriber = new TransactionSubscriber(signaler.Object, observer);

            blockSubscriber.Subscribe();

            signaler.Verify(s => s.Subscribe(observer), Times.Exactly(1));
        }
    }
}
