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
    public class BlockSubscriberTest
    {
        [Fact]
        public void SubscribeSubscribesObserverToSignaler()
        {
            var signaler = new Mock<ISignaler<Block>>();
            var observer = new BlockObserver(new Mock<IWalletSyncManager>().Object);
            var blockSubscriber = new BlockSubscriber(signaler.Object, observer);

            blockSubscriber.Subscribe();

            signaler.Verify(s => s.Subscribe(observer), Times.Exactly(1));
        }
    }
}
