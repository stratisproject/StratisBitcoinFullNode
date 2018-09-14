using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest : TestBase
    {
        private static readonly Script MinerScriptPubKey;

        public PeerBanningTest() : base(KnownNetworks.RegTest)
        {
        }

        static PeerBanningTest()
        {
            MinerScriptPubKey = new Key().ScriptPubKey;
        }

        private async Task<(TestChainContext context, IPEndPoint peerEndPoint)> InitialiseContextAndPeerEndpointAsync(Mock<IPeerAddressManager> mockPeerAddressManager = null)
        {
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, dataDir, mockPeerAddressManager);
            var peerEndPoint = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);
            context.PeerAddressManager.AddPeer(peerEndPoint, peerEndPoint.Address.MapToIPv6());

            return (context, peerEndPoint);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerDisconnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerDisconnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerDisconnected_ThePeerGetsBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock)
        {
            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync();
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>()))
                .Returns((INetworkPeer)null);

            Block badBlock = await createBadBlock(context);
            await context.Consensus.BlockMinedAsync(badBlock);

            Assert.True(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerDisconnected_AndAddressIsNull_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerDisconnectedAndAddressIsNull_ThePeerGetsBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerDisconnectedAndAddressIsNull_ThePeerGetsBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock)
        {
            var mockPeerAddressManager = new Mock<IPeerAddressManager>();
            mockPeerAddressManager.Setup(x => x.FindPeer(It.IsAny<IPEndPoint>())).Returns((PeerAddress)null);

            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync(mockPeerAddressManager);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>()))
                .Returns((INetworkPeer)null);

            Block badBlock = await createBadBlock(context);
            await context.Consensus.BlockMinedAsync(badBlock);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsConnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsConnected_ThePeerGetsBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsConnected_ThePeerGetsBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock)
        {
            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync();

            MockPeerConnection(context, false);

            Block badBlock = await createBadBlock(context);
            await context.Consensus.BlockMinedAsync(badBlock);

            Assert.True(context.PeerBanning.IsBanned(peerEndPoint));
        }

        private static void MockPeerConnection(TestChainContext context, bool whiteListedPeer)
        {
            var connectionManagerBehavior = new ConnectionManagerBehavior(context.ConnectionManager, context.LoggerFactory)
            { Whitelisted = whiteListedPeer };
            var peer = new Mock<INetworkPeer>();
            peer.Setup(p => p.Behavior<IConnectionManagerBehavior>()).Returns(connectionManagerBehavior);

            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(peer.Object);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async(
            Func<TestChainContext, Task<Block>> createBadBlock)
        {
            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync();

            MockPeerConnection(context, true);
            Block badBlock = await createBadBlock(context);
            await context.Consensus.BlockMinedAsync(badBlock);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndErrorIsNotBanError_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndErrorIsNotBanError_ThePeerIsNotBanned_Async(Func<TestChainContext, Task<Block>> createBadBlock)
        {
            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync();

            MockPeerConnection(context, false);
            Block badBlock = await createBadBlock(context);

            var blockValidationContext = new ValidationContext
            {
                BlockToValidate = badBlock,
                BanDurationSeconds = ValidationContext.BanDurationNoBan
            };

            await context.Consensus.BlockMinedAsync(badBlock);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsABlockWithBadPrevHashAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(
                Mine2BlocksAndCreateABlockWithBadPrevHashAsync);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task NodeIsSynced_PeerSendsAMutatedBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async()
        {
            await this.NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(
                MineAMutatedBlockAsync);
        }

        private async Task NodeIsSynced_PeerSendsABadBlockAndPeerIsBannedAndBanIsExpired_ThePeerIsNotBanned_Async(Func<TestChainContext, Task<Block>> createBadBlock)
        {
            (TestChainContext context, IPEndPoint peerEndPoint) = await this.InitialiseContextAndPeerEndpointAsync();

            MockPeerConnection(context, false);
            Block badBlock = await createBadBlock(context);

            var blockValidationContext = new ValidationContext
            {
                BlockToValidate = badBlock,
                BanDurationSeconds = 1,
            };

            await context.Consensus.BlockMinedAsync(badBlock);

            // Wait 1 sec for ban to expire.
            Thread.Sleep(1000);

            Assert.False(context.PeerBanning.IsBanned(peerEndPoint));
        }

        private static async Task<Block> Mine2BlocksAndCreateABlockWithBadPrevHashAsync(TestChainContext context)
        {
            List<Block> blocks = await TestChainFactory.MineBlocksAsync(context, 2, MinerScriptPubKey);

            Block block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            return block;
        }

        private static async Task<Block> MineAMutatedBlockAsync(TestChainContext context)
        {
            List<Block> blocks = await TestChainFactory.MineBlocksWithLastBlockMutatedAsync(context, 1, MinerScriptPubKey);
            Block block = blocks.Last();
            return block;
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task PeerBanning_AddingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act
            context.PeerBanning.BanAndDisconnectPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task PeerBanning_SavingAndLoadingBannedPeerToAddressManagerStoreAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act - Ban Peer, save store, clear current Peers, load store
            context.PeerBanning.BanAndDisconnectPeer(endpoint, context.ConnectionManager.ConnectionSettings.BanTimeSeconds, nameof(PeerBanningTest));
            context.PeerAddressManager.SavePeers();
            context.PeerAddressManager.Peers.Clear();
            context.PeerAddressManager.LoadPeers();

            // Assert
            PeerAddress peer = context.PeerAddressManager.FindPeer(endpoint);
            Assert.NotNull(peer.BanTimeStamp);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact(Skip = "Revisit with ConsensusManager tests")]
        public async Task PeerBanning_ResettingExpiredBannedPeerAsync()
        {
            // Arrange
            string dataDir = GetTestDirectoryPath(this);

            TestChainContext context = await TestChainFactory.CreateAsync(KnownNetworks.RegTest, dataDir);
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);
            context.PeerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            // Act
            context.PeerBanning.BanAndDisconnectPeer(endpoint, 1, nameof(PeerBanningTest));
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