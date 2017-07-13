using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;
using Stratis.Bitcoin.Connection;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Notifications
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
			var chainState = new Mock<ChainBehavior.ChainState>(new Mock<FullNode>().Object);
			var blockPuller = new Mock<LookaheadBlockPuller>(chain.Object, connectionManager.Object);
			var blockNotification = new Mock<BlockNotification>(chain.Object, blockPuller.Object, new Bitcoin.Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

			var blockNotificationFeature = new BlockNotificationFeature(blockNotification.Object, connectionManager.Object, blockPuller.Object, chainState.Object, chain.Object);
			blockNotificationFeature.Start();

			blockNotification.Verify(notif => notif.Notify(), Times.Once);
		}
	}
}