﻿using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Notifications.Tests
{
    public class BlockNotificationTest : LogsTestBase
    {
        public BlockNotificationTest()
            :base()
        {
            // These are expected to be false for non-POS test cases.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        /// <summary>
        /// Tests that <see cref="BlockNotification.Notify(System.Threading.CancellationToken)"/> exits due
        /// to <see cref="BlockNotification.StartHash"/> being null and no blocks were signaled.
        /// </summary>
        [Fact]
        public void Notify_Completes_StartHashNotSet()
        {
            var lifetime = new NodeLifetime();
            var signals = new Mock<ISignals>();
            var chain = new Mock<ConcurrentChain>();

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);
            notification.Notify(lifetime.ApplicationStopping);

            signals.Verify(s => s.SignalBlock(It.IsAny<Block>()), Times.Exactly(0));
        }

        /// <summary>
        /// Tests that <see cref="BlockNotification.Notify(System.Threading.CancellationToken)"/> exits due
        /// to <see cref="BlockNotification.StartHash"/> not being on the chain and no blocks were signaled.
        /// </summary>
        [Fact]
        public void Notify_Completes_StartHashNotOnChain()
        {
            var lifetime = new NodeLifetime();
            var signals = new Mock<ISignals>();

            var startBlockId = new uint256(156);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(startBlockId)).Returns((ChainedBlock)null);

            var notification = new BlockNotification(this.LoggerFactory.Object, chain.Object, new Mock<ILookaheadBlockPuller>().Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);
            notification.SyncFrom(startBlockId);
            notification.Notify(lifetime.ApplicationStopping);

            signals.Verify(s => s.SignalBlock(It.IsAny<Block>()), Times.Exactly(0));
        }

        /// <summary>
        /// Tests that <see cref="ILookaheadBlockPuller.Location"/> is set to the previous
        /// block's matching start hash.
        /// </summary>
        [Fact]
        public void Notify_WithSync_SetPullerLocationToPreviousBlockMatchingStartHash()
        {
            var blocks = this.CreateBlocks(3);

            var chain = new ConcurrentChain(blocks[0].Header);
            this.AppendBlocksToChain(chain, blocks.Skip(1).Take(2));

            var dataFolder = CreateDataFolder(this);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectedNodes).Returns(new NetworkPeerCollection());
            connectionManager.Setup(c => c.NodeSettings).Returns(new NodeSettings().LoadArguments(new string[] { $"-datadir={dataFolder.WalletPath}" }));
            connectionManager.Setup(c => c.Parameters).Returns(new NetworkPeerConnectionParameters());

            var lookAheadBlockPuller = new LookaheadBlockPuller(chain, connectionManager.Object, new Mock<ILoggerFactory>().Object);
            var lifetime = new NodeLifetime();

            var notification = new BlockNotification(this.LoggerFactory.Object, chain, lookAheadBlockPuller, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), lifetime);
            notification.SyncFrom(blocks[0].GetHash());
            notification.SyncFrom(blocks[0].GetHash());

            notification.Notify(lifetime.ApplicationStopping);

            Assert.Equal(0, lookAheadBlockPuller.Location.Height);
            Assert.Equal(lookAheadBlockPuller.Location.Header.GetHash(), blocks[0].Header.GetHash());
        }

        /// <summary>
        /// Ensures that <see cref="ISignals.SignalBlock(Block)" /> was called twice
        /// as 2 blocks were made available by the puller to be signaled.
        /// </summary>
        [Fact]
        public void Notify_WithSync_RunsAndBroadcastsBlocks()
        {
            var lifetime = new NodeLifetime();

            var blocks = this.CreateBlocks(2);

            var chain = new ConcurrentChain(blocks[0].Header);
            this.AppendBlocksToChain(chain, blocks.Skip(1).Take(1));

            var stub = new Mock<ILookaheadBlockPuller>();
            stub.SetupSequence(s => s.NextBlock(lifetime.ApplicationStopping))
                .Returns(new LookaheadResult { Block = blocks[0] })
                .Returns(new LookaheadResult { Block = blocks[1] })
                .Returns(null);

            var signals = new Mock<ISignals>();

            var notification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain, stub.Object, signals.Object, new AsyncLoopFactory(new LoggerFactory()), lifetime);
            notification.SetupGet(s => s.StartHash).Returns(blocks[0].GetHash());

            notification.SetupSequence(s => s.ReSync)
                .Returns(false)
                .Returns(false)
                .Returns(true);

            notification.Object.Notify(lifetime.ApplicationStopping);

            signals.Verify(s => s.SignalBlock(It.IsAny<Block>()), Times.Exactly(2));
        }

        /// <summary>
        /// Ensures that <see cref="BlockNotification.StartHash" /> gets updated
        /// every time <see cref="BlockNotification.SyncFrom(uint256)"/> gets called.
        /// </summary>
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
