using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest
    {
        public PeerBanningTest()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndPeerDiconnected_ThePeerGetsBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.NodeIsSynced_PeerSendsABadBlockAndPeerDiconnected_ThePeerGetsBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns((INetworkPeer)null);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peer });

            Assert.True(context.PeerBanning.IsBanned(peer));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory);
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);

            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peerEndPoint });
            Assert.True(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory) { Whitelisted = true };
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peerEndPoint });

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }
        

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory) { Whitelisted = true };
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peerEndPoint, BanDurationSeconds = BlockValidationContext.BanDurationNoBan });

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsBandAndBanIsExpired_ThePeerIsNotBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.NodeIsSynced_PeerSendsABadBlockAndPeerIsBandAndBanIsExpired_ThePeerIsNotBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory) { Whitelisted = true };
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<ConnectionManagerBehavior>()).Returns(connectionManagerBehavior);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peerEndPoint, BanDurationSeconds = 1 }); // ban for 1 second

            // wait 1 sec for ban to expire.
            Thread.Sleep(1000);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }
    }
}
