using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
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

        [Fact]
        public void PeerBanning_Add_Peer_To_Address_Manager_And_Ban()
        {
            var dataFolder = CreateDataFolder(this);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            var nodeSettings = new NodeSettings(new StratisRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var peerCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            peerCollection.Setup(p => p.FindByIp(It.IsAny<IPAddress>())).Returns(new List<INetworkPeer>());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectionSettings).Returns(connectionManagerSettings);
            connectionManager.Setup(c => c.ConnectedPeers).Returns(peerCollection.Object);

            var peerBanning = new PeerBanning(connectionManager.Object, loggerFactory, DateTimeProvider.Default, peerAddressManager);
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTest));

            PeerAddress peer = peerAddressManager.FindPeer(endpoint);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public void PeerBanning_Add_WhiteListed_Peer_Does_Not_Get_Banned()
        {
            var dataFolder = CreateDataFolder(this);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.AddPeer(endpoint, endpoint.Address.MapToIPv6());

            var nodeSettings = new NodeSettings(new StratisRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var connectionManagerBehaviour = new Mock<IConnectionManagerBehavior>();
            connectionManagerBehaviour.Setup(c => c.Whitelisted).Returns(true);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.Setup(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.Setup(n => n.Behavior<IConnectionManagerBehavior>()).Returns(connectionManagerBehaviour.Object);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(n => n.CreateConnectedNetworkPeerAsync(endpoint, null, null)).ReturnsAsync(networkPeer.Object);

            var peerCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            peerCollection.Setup(p => p.FindByIp(It.IsAny<IPAddress>())).Returns(new List<INetworkPeer>() { networkPeer.Object });

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectionSettings).Returns(connectionManagerSettings);
            connectionManager.Setup(c => c.ConnectedPeers).Returns(peerCollection.Object);

            var peerBanning = new PeerBanning(connectionManager.Object, loggerFactory, DateTimeProvider.Default, peerAddressManager);
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTest));

            // Peer is whitelised and will not be banned.
            PeerAddress peer = peerAddressManager.FindPeer(endpoint);
            Assert.False(peer.BanUntil.HasValue);
            Assert.Null(peer.BanUntil);
            Assert.Null(peer.BanReason);
        }

        [Fact]
        public void PeerBanning_Add_Peers_To_Address_Manager_And_Ban_IP_Range()
        {
            var dataFolder = CreateDataFolder(this);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            var ipAddress80 = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint80 = new IPEndPoint(ipAddress80, 80);

            var ipAddress81 = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint81 = new IPEndPoint(ipAddress81, 81);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.AddPeer(endpoint80, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpoint81, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(new StratisRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var peerCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            peerCollection.Setup(p => p.FindByIp(It.IsAny<IPAddress>())).Returns(new List<INetworkPeer>());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectionSettings).Returns(connectionManagerSettings);
            connectionManager.Setup(c => c.ConnectedPeers).Returns(peerCollection.Object);

            var peerBanning = new PeerBanning(connectionManager.Object, loggerFactory, DateTimeProvider.Default, peerAddressManager);
            peerBanning.BanAndDisconnectPeer(endpoint80, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTest));

            // Both endpoints should be banned.
            PeerAddress peer = peerAddressManager.FindPeer(endpoint80);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);

            peer = peerAddressManager.FindPeer(endpoint81);
            Assert.True(peer.BanUntil.HasValue);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public void PeerBanning_SavingAndLoading_BannedPeerToAddressManager()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.AddPeer(endpoint, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(new StratisRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var peerCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            peerCollection.Setup(p => p.FindByIp(It.IsAny<IPAddress>())).Returns(new List<INetworkPeer>());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectionSettings).Returns(connectionManagerSettings);
            connectionManager.Setup(c => c.ConnectedPeers).Returns(peerCollection.Object);

            var peerBanning = new PeerBanning(connectionManager.Object, loggerFactory, DateTimeProvider.Default, peerAddressManager);
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTest));

            peerAddressManager.SavePeers();
            peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.LoadPeers();

            PeerAddress peer = peerAddressManager.FindPeer(endpoint);
            Assert.NotNull(peer.BanTimeStamp);
            Assert.NotNull(peer.BanUntil);
            Assert.NotEmpty(peer.BanReason);
        }

        [Fact]
        public void PeerBanning_Resetting_Expired_BannedPeer()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.AddPeer(endpoint, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(new StratisRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var peerCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            peerCollection.Setup(p => p.FindByIp(It.IsAny<IPAddress>())).Returns(new List<INetworkPeer>());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.ConnectionSettings).Returns(connectionManagerSettings);
            connectionManager.Setup(c => c.ConnectedPeers).Returns(peerCollection.Object);

            var peerBanning = new PeerBanning(connectionManager.Object, loggerFactory, DateTimeProvider.Default, peerAddressManager);
            peerBanning.BanAndDisconnectPeer(endpoint, 1, nameof(PeerBanningTest));

            peerAddressManager.SavePeers();

            // Wait one second for ban to expire.
            Thread.Sleep(1000);

            peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.LoadPeers();

            PeerAddress peer = peerAddressManager.FindPeer(endpoint);
            Assert.Null(peer.BanTimeStamp);
            Assert.Null(peer.BanUntil);
            Assert.Empty(peer.BanReason);
        }
    }
}