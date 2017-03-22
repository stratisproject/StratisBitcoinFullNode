using System;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Notifications;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class BlockNotificationFeatureTest : LogsTestBase
    {
        [Fact]
        public void BlockNotificationFeatureCallsNotifyOnStart()
        {
            var cancellationProvider = new FullNode.CancellationProvider
            {
                Cancellation = new CancellationTokenSource()
            };
            var blockNotification = new Mock<BlockNotification>(new ConcurrentChain(), new Mock<ILookaheadBlockPuller>().Object, new Signals());

            var blockNotificationFeature = new BlockNotificationFeature(blockNotification.Object, new BlockNotificationStartHash(0), cancellationProvider);
            blockNotificationFeature.Start();

            blockNotification.Verify(notif => notif.Notify(0, cancellationProvider.Cancellation.Token), Times.Once);
        }
    }
}
