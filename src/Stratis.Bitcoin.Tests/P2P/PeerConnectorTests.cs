using System;
using System.IO;
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
        private readonly INetworkPeerFactory networkPeerFactory;
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
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressTwo, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings();
            nodeSettings.LoadConfiguration();

            var connectionManagerSettings = new ConnectionManagerSettings();
            connectionManagerSettings.Load(nodeSettings);
            connectionManagerSettings.AddNode.Add(endpointAddNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressOne, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressOne);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker());

            var connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.Contains(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorAddNode_CanAlwaysStart()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var nodeSettings = new NodeSettings();
            nodeSettings.LoadConfiguration();

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var connector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnectNode_ConnectsTo_ConnectNodePeers()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            var ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            var ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings();

            var connectionManagerSettings = new ConnectionManagerSettings();
            connectionManagerSettings.Load(nodeSettings);
            connectionManagerSettings.Connect.Add(endpointConnectNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressConnect, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressConnect);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker());

            var connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointConnectNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorConnect_WithConnectPeersSpecified_CanStart()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressThree, 80);

            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            connectionSettings.Connect.Add(endpointConnectNode);

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_WithNoConnectPeersSpecified_CanNotStart()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());
            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_DiscoveredPeers()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var ipAddressAdd = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressAdd, 80);

            var ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            var ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings();

            var connectionManagerSettings = new ConnectionManagerSettings();
            connectionManagerSettings.Load(nodeSettings);

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

            var connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointConnectNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorDiscover_WithNoConnectPeersSpecified_CanStart()
        {
            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscover_WithConnectPeersSpecified_CanNotStart()
        {
            var nodeSettings = new NodeSettings();

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new IPEndPoint(ipAddressThree, 80);

            connectionSettings.Connect.Add(networkAddressConnectNode);

            var peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker());
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void ConnectAsync_WithASelfConnectionAttempt_DoesNotAttemptToConnect()
        {
            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();
            selfEndpointTracker.Setup(x => x.IsSelf(It.IsAny<IPEndPoint>())).Returns(true);
            var peerAddressManager = new Mock<IPeerAddressManager>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, new NodeSettings(), new ConnectionManagerSettings(), peerAddressManager.Object, selfEndpointTracker.Object);

            peerConnector.ConnectAsync(new PeerAddress()).GetAwaiter().GetResult();

            peerAddressManager.Verify(x => x.PeerAttempted(It.IsAny<IPEndPoint>(), It.IsAny<DateTime>()), Times.Never());
        }

        private IConnectionManager CreateConnectionManager(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager, IPeerConnector peerConnector)
        {
            var connectionManager = new ConnectionManager(
                DateTimeProvider.Default,
                this.LoggerFactory.Object,
                this.network,
                this.networkPeerFactory,
                nodeSettings,
                this.nodeLifetime,
                this.networkPeerParameters,
                peerAddressManager,
                new IPeerConnector[] { peerConnector },
                null,
                connectionSettings);

            return connectionManager;
        }
    }
}