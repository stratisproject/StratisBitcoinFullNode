using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Signals;
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
