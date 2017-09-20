using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Notifications.Tests
{
    public class BlockNotificationFeatureTest : LogsTestBase
    {
        [Fact]
        public void BlockNotificationFeatureCallsNotifyOnStart()
        {
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectedNodes)
                .Returns(new NodesCollection());
            connectionManager.Setup(c => c.NodeSettings)
                .Returns(NodeSettings.Default());
            connectionManager.Setup(c => c.Parameters)
                .Returns(new NodeConnectionParameters());

            var chain = new Mock<ConcurrentChain>();
            var chainState = new Mock<ChainState>(new Mock<FullNode>().Object);
            var blockPuller = new Mock<LookaheadBlockPuller>(chain.Object, connectionManager.Object, new LoggerFactory());
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, blockPuller.Object, new Bitcoin.Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            var blockNotificationFeature = new BlockNotificationFeature(blockNotification.Object, connectionManager.Object, blockPuller.Object, chainState.Object, chain.Object);
            blockNotificationFeature.Start();

            blockNotification.Verify(notif => notif.Notify(), Times.Once);
        }
    }
}