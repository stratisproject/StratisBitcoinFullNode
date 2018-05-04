using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest : TestBase
    {
        public PeerBanningTest() : base(Network.RegTest)
        {
        }

        [Fact]
        public async Task NodeIsSynced_PeerSendsABadBlockAndPeerDiconnected_ThePeerGetsBanned_Async()
        {
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);
            context.PeerAddressManager.AddPeer(peer, peer.Address.MapToIPv6());

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
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);
            context.PeerAddressManager.AddPeer(peerEndPoint, peerEndPoint.Address.MapToIPv6());

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
            string dataDir = GetTestDirectoryPath(this);

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
            string dataDir = GetTestDirectoryPath(this);

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
            string dataDir = GetTestDirectoryPath(this);
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

        [Fact]
        public async Task PeerBanning_AddingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange 
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act
            context.PeerBanning.BanPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public async Task PeerBanning_SavingAndLoadingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange 
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act - Ban Peer, save store, clear current Peers, load store
            context.PeerBanning.BanPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));
            context.PeerAddressManager.SavePeers();
            context.PeerAddressManager.Peers.Clear();
            context.PeerAddressManager.LoadPeers();

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.NotNull(peer.BanTimeStamp);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public async Task PeerBanning_ResettingExpiredBannedPeerAsync()
        {
            // Arrange 
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act 
            context.PeerBanning.BanPeer(endpoint, 1, nameof(PeerBanningTest));
            context.PeerAddressManager.SavePeers();

            // Wait one second for ban to expire.
            Thread.Sleep(1000);

            context.PeerAddressManager.Peers.Clear();
            context.PeerAddressManager.LoadPeers();

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.Null(peer.BanTimeStamp);
            Assert.Null(peer.BanUntil);
            Assert.Empty(peer.BanReason);
        }
    }
}
