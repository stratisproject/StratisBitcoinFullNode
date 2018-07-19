using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests : LogsTestBase
    {
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly ExtendedLoggerFactory extendedLoggerFactory;
        private readonly NetworkPeerConnectionParameters networkPeerParameters;
        private readonly NodeLifetime nodeLifetime;
        private readonly Network network;

        public PeerConnectorTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();

            this.asyncLoopFactory = new AsyncLoopFactory(this.extendedLoggerFactory);
            this.network = Network.Main;
            this.networkPeerParameters = new NetworkPeerConnectionParameters();

            this.nodeLifetime = new NodeLifetime();
        }

        [Fact]
        public void PeerConnectorAddNode_ConnectsTo_AddNodePeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            IPAddress ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            IPAddress ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressTwo, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(Network.Main, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            connectionManagerSettings.AddNode.Add(endpointAddNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressOne, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressOne);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.Contains(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorAddNode_CanAlwaysStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var connector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnectNode_ConnectsTo_ConnectNodePeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            IPAddress ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            IPAddress ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            IPAddress ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(Network.Main, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            connectionManagerSettings.Connect.Add(endpointConnectNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressConnect, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressConnect);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointConnectNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorConnect_WithConnectPeersSpecified_CanStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            IPAddress ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressThree, 80);

            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            connectionSettings.Connect.Add(endpointConnectNode);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_WithNoConnectPeersSpecified_CanNotStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var nodeSettings = new NodeSettings();
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_DiscoveredPeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            IPAddress ipAddressAdd = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressAdd, 80);

            IPAddress ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            IPAddress ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(Network.Main, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            connectionManagerSettings.AddNode.Add(endpointAddNode);
            connectionManagerSettings.Connect.Add(endpointConnectNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressDiscovered, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressDiscovered);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointConnectNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_NodeInSameNetworkGroup_WithIpRangeFilteringDisabled()
        {
            Network mainNetwork = Network.Main;
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            // IpRangeFiltering enabled by default, disabled explicitly.
            var nodeSettings = new NodeSettings(mainNetwork, args: new[] { "-IpRangeFiltering=false" });
            
            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);
            
            // Peer 1.
            IPAddress originalAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointNode1 = new IPEndPoint(originalAddress, 80);
            peerAddressManager.AddPeer(endpointNode1, IPAddress.Loopback);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointNode1);
            Assert.True(connectedToThisPeer);

            // Peer 2 has different network group.
            IPAddress addressInDifferentNetworkGroup = IPAddress.Parse("193.168.0.1"); // ipv4
            var endpointNode2 = new IPEndPoint(addressInDifferentNetworkGroup, 80);
            peerAddressManager.AddPeer(endpointNode2, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointNode2);
            Assert.True(connectedToThisPeer);  // Different network group: connects.

            // Peer 3 has different network group.
            IPAddress addressInSameNetworkGroupPeer3 = IPAddress.Parse("::ffff:194.168.0.1");
            var endpointNode3 = new IPEndPoint(addressInSameNetworkGroupPeer3, 80);
            peerAddressManager.AddPeer(endpointNode3, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointNode3);
            Assert.True(connectedToThisPeer); // Same network group: connects.

            // Peer 4 has same network group.
            IPAddress addressInSameNetworkGroupPeer4 = IPAddress.Parse("192.168.1.0"); // ipv4
            var endpointNode4 = new IPEndPoint(addressInSameNetworkGroupPeer4, 80);
            peerAddressManager.AddPeer(endpointNode4, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointNode4);
            Assert.True(connectedToThisPeer); // Same network group: connects.

            // Peer 5 has same network group.
            IPAddress addressInSameNetworkGroupPeer5 = IPAddress.Parse("::ffff:192.168.1.1");
            var endpointNode5 = new IPEndPoint(addressInSameNetworkGroupPeer5, 80);
            peerAddressManager.AddPeer(endpointNode5, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointNode5);
            Assert.True(connectedToThisPeer); // Same network group: connects.
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_LocalHostNodes_IpRangeFilteringEnabled()
        {
            // Setup.
            Network mainNetwork = Network.Main;
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var nodeSettings = new NodeSettings(mainNetwork, args: new[] { "-IpRangeFiltering" });

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);

            // Peer 1 on localhost.
            IPAddress originalAddress = IPAddress.Parse("::ffff:127.0.0.1");
            var originalLocalhostNodeEndpoint = new IPEndPoint(originalAddress, 80);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, originalLocalhostNodeEndpoint);
            Assert.True(connectedToThisPeer);

            // Peer 2 on localhost on different port.
            IPAddress addressInSameNetworkGroupIpv4 = IPAddress.Parse("127.0.0.1"); // ipv4
            var secondLocalhostNodeEndpoint = new IPEndPoint(addressInSameNetworkGroupIpv4, 90);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, secondLocalhostNodeEndpoint);
            Assert.True(connectedToThisPeer);

            // Peer 3 on localhost on different port.
            IPAddress addressInSameNetworkGroupIpv6 = IPAddress.Parse("::ffff:127.0.0.1");
            secondLocalhostNodeEndpoint = new IPEndPoint(addressInSameNetworkGroupIpv6, 99);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, secondLocalhostNodeEndpoint);
            Assert.True(connectedToThisPeer);
        }

        [Fact]
        public void PeerConnectorDiscovery_DoesNotConnectTo_NodeInSameNetworkGroup_WithIpRangeFilteringEnabled()
        {
            // Setup.
            Network mainNetwork = Network.Main;
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var nodeSettings = new NodeSettings(mainNetwork, args: new[] { "-IpRangeFiltering" });

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);

            // Original peer.
            IPAddress originalAddress = IPAddress.Parse("192.168.0.1"); // ipv4
            var originalNodeEndpoint = new IPEndPoint(originalAddress, 80);
            bool connectToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, originalNodeEndpoint);
            Assert.True(connectToThisPeer);
            
            // Peer from same group.
            IPAddress addressFromNodeInSameGroup = IPAddress.Parse("192.168.0.2"); // ipv4
            var endpointOfNodeInSameGroup = new IPEndPoint(addressFromNodeInSameGroup, 80);
            connectToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointOfNodeInSameGroup);
            Assert.False(connectToThisPeer);

            // Peer from same group.
            addressFromNodeInSameGroup = IPAddress.Parse("::ffff:192.168.0.3");
            endpointOfNodeInSameGroup = new IPEndPoint(addressFromNodeInSameGroup, 80);
            connectToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointOfNodeInSameGroup);
            Assert.False(connectToThisPeer);

            // Peer from different group.
            IPAddress addressFromNodeInDifferentGroup = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointOfNodeInDifferentGroup = new IPEndPoint(addressFromNodeInDifferentGroup, 80);
            connectToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointOfNodeInDifferentGroup);
            Assert.True(connectToThisPeer);
        }

        private static bool ConnectToPeer(PeerAddressManager peerAddressManager, Mock<INetworkPeerFactory> networkPeerFactoryExisting, ConnectionManagerSettings connectionManagerSettingsExisting, PeerConnectorConnectNode peerConnector, IPEndPoint endpointNode)
        {
            peerAddressManager.AddPeer(endpointNode, IPAddress.Loopback);
            Mock<INetworkPeer> networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(endpointNode.Address, endpointNode.Port));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(endpointNode.Address);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(endpointNode.Port);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);
            networkPeerFactoryExisting.Setup(npf =>
                npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode)), 
                    It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));
            connectionManagerSettingsExisting.AddNode.Add(endpointNode);
            connectionManagerSettingsExisting.Connect.Add(endpointNode);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();
            return peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint).Contains(endpointNode);
        }

        [Fact]
        public void PeerConnectorDiscover_WithNoConnectPeersSpecified_CanStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var nodeSettings = new NodeSettings();
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscover_WithConnectPeersSpecified_CanNotStart()
        {
            var nodeSettings = new NodeSettings();
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new IPEndPoint(ipAddressThree, 80);

            connectionSettings.Connect.Add(networkAddressConnectNode);

            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void ConnectAsync_WithASelfConnectionAttempt_DoesNotAttemptToConnect()
        {
            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();
            selfEndpointTracker.Setup(x => x.IsSelf(It.IsAny<IPEndPoint>())).Returns(true);
            var peerAddressManager = new Mock<IPeerAddressManager>();
            var nodeSettings = new NodeSettings();
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, new ConnectionManagerSettings(nodeSettings), peerAddressManager.Object, selfEndpointTracker.Object);

            peerConnector.ConnectAsync(new PeerAddress()).GetAwaiter().GetResult();

            peerAddressManager.Verify(x => x.PeerAttempted(It.IsAny<IPEndPoint>(), It.IsAny<DateTime>()), Times.Never());
        }

        private IConnectionManager CreateConnectionManager(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager, IPeerConnector peerConnector)
        {
            var networkPeerFactory = new Mock<INetworkPeerFactory>();

            var connectionManager = new ConnectionManager(
                DateTimeProvider.Default,
                this.LoggerFactory.Object,
                this.network,
                networkPeerFactory.Object,
                nodeSettings,
                this.nodeLifetime,
                this.networkPeerParameters,
                peerAddressManager,
                new IPeerConnector[] { peerConnector },
                null,
                connectionSettings, 
                new VersionProvider());

            return connectionManager;
        }
    }
}