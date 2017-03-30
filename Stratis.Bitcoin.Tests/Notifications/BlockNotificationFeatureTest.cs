using System;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Notifications;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.BlockStore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;

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

			var connectionManager = new Mock<ConnectionManager>(Network.Main, new Mock<NodeConnectionParameters>().Object, new Mock<NodeSettings>().Object);
			var chain = new Mock<ConcurrentChain>();
			var chainState = new Mock<ChainBehavior.ChainState>(new Mock<FullNode>().Object);
			var blockPuller = new Mock<LookaheadBlockPuller>(chain.Object, connectionManager.Object);
			var blockNotification = new Mock<BlockNotification>(chain.Object, blockPuller.Object, new Signals());

			var blockNotificationFeature = new BlockNotificationFeature(blockNotification.Object, new BlockNotificationStartHash(0), cancellationProvider, connectionManager.Object, blockPuller.Object, chainState.Object, chain.Object);
			blockNotificationFeature.Start();

			blockNotification.Verify(notif => notif.Notify(cancellationProvider.Cancellation.Token), Times.Once);
		}
	}
}