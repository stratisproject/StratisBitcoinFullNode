using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Notifications.Tests
{
    public class BlockNotificationTest : LogsTestBase
    {
        [Fact]
        public void NotifyStartHashNotOnChainCompletes()
        {
            var startBlockId = new uint256(156);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(startBlockId))
                .Returns((ChainedBlock)null);

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, new Bitcoin.Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            notification.Notify();
        }

        [Fact]
        public void NotifySetsPullerLocationToPreviousBlockMatchingStartHash()
        {
            var blockId0 = new uint256(150);
            var header0 = new BlockHeader();

            var blockId1 = new uint256(151);
            var header1 = new BlockHeader("00000020e84aee5c9c09f7eb37e0654ba0b3e5a2825c7866bd20409479451a4100000000fe30d12ce0075d539de3d8c15c59767ed9c4299fc93fd5c4919190a72d8dbb9583ffc059ffff001d1140956c");

            var blockId2 = new uint256(152);
            var header2 = new BlockHeader("00000020cccdc29c36a75b1b4226f218029bac0f3ddd4262ab787419b04b72c600000000b1834fd70f594e0a47d2048a770de5f9eccedb8ea8ff10b1be66c354fd4be28ccdfac059ffff001d07a85047");

            var chain = new Mock<ConcurrentChain>();

            chain.Setup(c => c.GetBlock(0)).Returns(new ChainedBlock(header0, 0));
            chain.Setup(c => c.GetBlock(1)).Returns(new ChainedBlock(header1, 1));
            chain.Setup(c => c.GetBlock(2)).Returns(new ChainedBlock(header2, 2));
            chain.Setup(c => c.GetBlock(blockId0)).Returns(new ChainedBlock(header0, 0));
            chain.Setup(c => c.GetBlock(blockId1)).Returns(new ChainedBlock(header1, 1));
            chain.Setup(c => c.GetBlock(blockId2)).Returns(new ChainedBlock(header2, 2));

            var stub = new Mock<ILookaheadBlockPuller>();
            var lifetime = new NodeLifetime();
            stub.Setup(s => s.NextBlock(lifetime.ApplicationStopping)).Returns((Block)null);

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, stub.Object, new Bitcoin.Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), lifetime);

            notification.Notify();
            notification.SyncFrom(blockId1);
            notification.SyncFrom(blockId1);
            stub.Verify(s => s.SetLocation(It.Is<ChainedBlock>(c => c.Height == 0 && c.Header.GetHash() == header0.GetHash())));
        }

        [Fact]
        public async Task NotifyWithoutSyncFromRunsWithoutBroadcastingBlocks()
        {
            var lifetime = new NodeLifetime();
            new CancellationTokenSource(100).Token.Register(() => lifetime.StopApplication());

            var startBlockId = new uint256(156);
            var chain = new Mock<ConcurrentChain>();
            var header = new BlockHeader();
            chain.Setup(c => c.GetBlock(startBlockId))
                .Returns(new ChainedBlock(header, 0));
            var stub = new Mock<ILookaheadBlockPuller>();
            stub.SetupSequence(s => s.NextBlock(lifetime.ApplicationStopping))
                .Returns(new Block())
                .Returns(new Block())
                .Returns((Block)null);

            var signals = new Mock<ISignals>();

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, stub.Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);

            await notification.Notify().RunningTask;

            signals.Verify(s => s.SignalBlock(It.IsAny<Block>()), Times.Exactly(0));
        }

        [Fact]
        public async Task NotifyWithSyncFromSetBroadcastsOnNextBlock()
        {
            var lifetime = new NodeLifetime();
            new CancellationTokenSource(100).Token.Register(() => lifetime.StopApplication());

            var startBlockId = new uint256(156);
            var chain = new Mock<ConcurrentChain>();
            var header = new BlockHeader();
            chain.Setup(c => c.GetBlock(startBlockId))
                .Returns(new ChainedBlock(header, 0));

            var stub = new Mock<ILookaheadBlockPuller>();
            stub.SetupSequence(s => s.NextBlock(lifetime.ApplicationStopping))
                .Returns(new Block())
                .Returns(new Block())
                .Returns((Block)null);

            var signals = new Mock<ISignals>();

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, stub.Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);

            notification.SyncFrom(startBlockId);
            await notification.Notify().RunningTask;

            signals.Verify(s => s.SignalBlock(It.IsAny<Block>()), Times.Exactly(2));
        }

        [Fact]
        public void CallingSyncFromUpdatesStartHashAccordingly()
        {
            var lifetime = new NodeLifetime();
            var chain = new Mock<ConcurrentChain>();
            var stub = new Mock<ILookaheadBlockPuller>();
            var signals = new Mock<ISignals>();

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, stub.Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);

            var blockId1 = new uint256(150);
            var blockId2 = new uint256(151);

            Assert.Null(notification.StartHash);
            notification.SyncFrom(blockId1);

            Assert.NotNull(notification.StartHash);
            Assert.Equal(blockId1, notification.StartHash);

            notification.SyncFrom(blockId2);
            Assert.Equal(blockId2, notification.StartHash);
        }
    }
}