using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Tests.Notifications
{
	public class BlockNotificationTest
	{
		private CancellationTokenSource source;

		public BlockNotificationTest()
		{
			this.source = new CancellationTokenSource();
		}

		[Fact]
		public void NotifyStartHashNotOnChainCompletes()
		{

			var startBlockId = new uint256(156);
			var chain = new Mock<ConcurrentChain>();
			chain.Setup(c => c.GetBlock(startBlockId))
				.Returns((ChainedBlock)null);
			
			var notification = new BlockNotification(chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Signals());

			notification.Notify(this.source.Token);			
		}

		[Fact]
		public void NotifySetsPullerLocationToBlockMatchingStartHash()
		{
			var startBlockId = new uint256(156);
			var chain = new Mock<ConcurrentChain>();
			var header = new BlockHeader();
			chain.Setup(c => c.GetBlock(startBlockId))
				.Returns(new ChainedBlock(header, 0));

			var stub = new Mock<ILookaheadBlockPuller>();
			stub.Setup(s => s.NextBlock(this.source.Token))
				.Returns((Block)null);

			var notification = new BlockNotification(chain.Object, stub.Object, new Signals());

			notification.Notify(this.source.Token);
			notification.SyncFrom(startBlockId);
		    notification.SyncFrom(startBlockId);
            stub.Verify(s => s.SetLocation(It.Is<ChainedBlock>(c => c.Height == 0 && c.Header.GetHash() == header.GetHash())));
		}

		[Fact]
		public void NotifyBroadcastsOnNextBlock()
		{
			var startBlockId = new uint256(156);
			var chain = new Mock<ConcurrentChain>();
			var header = new BlockHeader();
			chain.Setup(c => c.GetBlock(startBlockId))
				.Returns(new ChainedBlock(header, 0));

			var stub = new Mock<ILookaheadBlockPuller>();
			stub.SetupSequence(s => s.NextBlock(this.source.Token))
				.Returns(new Block())
				.Returns(new Block())
				.Returns((Block)null);

			var signals = new Mock<ISignals>();
			var signalerMock = new Mock<ISignaler<Block>>();
			signals.Setup(s => s.Blocks)
				.Returns(signalerMock.Object);

			var notification = new BlockNotification(chain.Object, stub.Object, signals.Object);

			notification.Notify(this.source.Token);
			notification.SyncFrom(startBlockId);

			Thread.Sleep(100);
			signalerMock.Verify(s => s.Broadcast(It.IsAny<Block>()), Times.Exactly(2));
		}
	}
}