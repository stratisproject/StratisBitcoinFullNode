using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerBanningTests : TestBase
    {
        public PeerBanningTests() : base(new StratisRegTest())
        {
        }

        public class PowNetworkWithMaxReorg : BitcoinMain
        {
            public PowNetworkWithMaxReorg()
            {
                this.Name = Guid.NewGuid().ToString();

                Type consensusType = typeof(NBitcoin.Consensus);
                consensusType.GetProperty("MaxReorgLength").SetValue(this.Consensus, (uint)20);
            }
        }

        [Fact]
        public void Calculate_BanTime_BasedOn_Pow_Network_Without_MaxReorg()
        {
            var powNetwork = new BitcoinMain();
            var nodeSettings = NodeSettings.Default(powNetwork);
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            var calculatedBanTime = powNetwork.Consensus.PowTargetSpacing.TotalSeconds * NBitcoin.Network.FallBackMaxReorg / 2;
            Assert.Equal((int)calculatedBanTime, connectionManagerSettings.BanTimeSeconds);
        }

        [Fact]
        public void Calculate_BanTime_BasedOn_Pow_Network_With_MaxReorg()
        {
            var powNetwork = new PowNetworkWithMaxReorg();

            var nodeSettings = NodeSettings.Default(powNetwork);
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            var calculatedBanTime = powNetwork.Consensus.PowTargetSpacing.TotalSeconds * powNetwork.Consensus.MaxReorgLength / 2;
            Assert.Equal((int)calculatedBanTime, connectionManagerSettings.BanTimeSeconds);
        }

        [Fact]
        public void Calculate_BanTime_BasedOn_Pos_Network()
        {
            var nodeSettings = NodeSettings.Default(this.Network);
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            var calculatedBanTime = this.Network.Consensus.Options.TargetSpacingSeconds * this.Network.Consensus.MaxReorgLength / 2;
            Assert.Equal((int)calculatedBanTime, connectionManagerSettings.BanTimeSeconds);
        }

        [Fact]
        public void Calculate_BanTime_CommandLine_Overrides()
        {
            var nodeSettings = new NodeSettings(network: this.Network, args: new[] { "-bantime=1000" });
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            Assert.Equal(1000, connectionManagerSettings.BanTimeSeconds);
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
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTests));

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
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTests));

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
            peerBanning.BanAndDisconnectPeer(endpoint80, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTests));

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
            peerBanning.BanAndDisconnectPeer(endpoint, connectionManagerSettings.BanTimeSeconds, nameof(PeerBanningTests));

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
            peerBanning.BanAndDisconnectPeer(endpoint, 1, nameof(PeerBanningTests));

            peerAddressManager.SavePeers();

            // Wait one second for ban to expire.
            Thread.Sleep(1000);

            peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, loggerFactory, new Mock<ISelfEndpointTracker>().Object);
            peerAddressManager.LoadPeers();

            PeerAddress peer = peerAddressManager.FindPeer(endpoint);
            Assert.Null(peer.BanTimeStamp);
            Assert.Null(peer.BanUntil);
            Assert.Null(peer.BanReason);
        }
    }
}
