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

            var nodeSettings = new NodeSettings();

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

            var nodeSettings = new NodeSettings();

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

            var nodeSettings = new NodeSettings();

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
            Network testNetwork = Network.Main;
            var nodeSettings = new NodeSettings(testNetwork, args: new[] { "-IpRangeFilteringDisabled" });
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            // Peer 1.
            IPAddress originalAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointNode1 = new IPEndPoint(originalAddress, 80);
            peerAddressManager.AddPeer(endpointNode1, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer1 = new Mock<INetworkPeer>();
            networkPeer1.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(originalAddress, 80));
            networkPeer1.SetupGet(np => np.RemoteSocketAddress).Returns(originalAddress);
            networkPeer1.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer1.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            // Peer 2 has different network group.
            IPAddress addressInDifferentNetworkGroup = IPAddress.Parse("::ffff:193.168.0.1");
            var endpointNode2 = new IPEndPoint(addressInDifferentNetworkGroup, 80);
            peerAddressManager.AddPeer(endpointNode2, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer2 = new Mock<INetworkPeer>();
            networkPeer2.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(addressInDifferentNetworkGroup, 80));
            networkPeer2.SetupGet(np => np.RemoteSocketAddress).Returns(addressInDifferentNetworkGroup);
            networkPeer2.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer2.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            // Peer 3 has same network group.
            IPAddress addressInSameNetworkGroup = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointNode3 = new IPEndPoint(addressInSameNetworkGroup, 80);
            peerAddressManager.AddPeer(endpointNode3, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer3 = new Mock<INetworkPeer>();
            networkPeer3.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(addressInSameNetworkGroup, 80));
            networkPeer3.SetupGet(np => np.RemoteSocketAddress).Returns(addressInSameNetworkGroup);
            networkPeer3.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer3.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode1);
            connectionManagerSettingsExisting.Connect.Add(endpointNode1);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode2);
            connectionManagerSettingsExisting.Connect.Add(endpointNode2);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode3);
            connectionManagerSettingsExisting.Connect.Add(endpointNode3);

            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode1)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer1.Object));
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode2)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer2.Object));
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode3)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer3.Object));

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            // Connects to nodes from a different network group.
            Assert.Contains(endpointNode1, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointNode2, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));

            // Connects to nodes in the same network group.
            Assert.Contains(endpointNode3, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_LocalHostNodes_WithIpRangeFilteringEnabled()
        {
            Network testNetwork = Network.Main;
            var nodeSettings = new NodeSettings(testNetwork, args: new[] { string.Empty }); // No IpRangeFilteringDisabled arg.
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            // Peer 1 on localhost.
            IPAddress originalAddress = IPAddress.Parse("::ffff:127.0.0.1");
            var endpointNode1 = new IPEndPoint(originalAddress, 80);
            peerAddressManager.AddPeer(endpointNode1, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer1 = new Mock<INetworkPeer>();
            networkPeer1.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(originalAddress, 80));
            networkPeer1.SetupGet(np => np.RemoteSocketAddress).Returns(originalAddress);
            networkPeer1.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer1.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            // Peer 2 on localhost on different port.
            IPAddress addressInSameNetworkGroup = IPAddress.Parse("::ffff:127.0.0.1");
            var endpointNode2 = new IPEndPoint(addressInSameNetworkGroup, 90);
            peerAddressManager.AddPeer(endpointNode2, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer2 = new Mock<INetworkPeer>();
            networkPeer2.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(addressInSameNetworkGroup, 90));
            networkPeer2.SetupGet(np => np.RemoteSocketAddress).Returns(addressInSameNetworkGroup);
            networkPeer2.SetupGet(np => np.RemoteSocketPort).Returns(90);
            networkPeer2.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode1);
            connectionManagerSettingsExisting.Connect.Add(endpointNode1);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode2);
            connectionManagerSettingsExisting.Connect.Add(endpointNode2);

            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode1)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer1.Object));
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode2)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer2.Object));
            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            // Connects to localhost nodes even with IP range filtering enabled.
            Assert.Contains(endpointNode1, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointNode2, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }
        
        [Fact]
        public void PeerConnectorDiscovery_DoesNotConnectTo_NodeInSameNetworkGroup_WithIpRangeFilteringEnabled()
        {
            Network testNetwork = Network.Main;
            var nodeSettings = new NodeSettings(testNetwork, args: new[] { string.Empty });  // No IpRangeFilteringDisabled arg.
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker());

            // Peer 1.
            IPAddress originalAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointNode1 = new IPEndPoint(originalAddress, 80);           
            peerAddressManager.AddPeer(endpointNode1, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer1 = new Mock<INetworkPeer>();
            networkPeer1.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(originalAddress, 80));
            networkPeer1.SetupGet(np => np.RemoteSocketAddress).Returns(originalAddress);
            networkPeer1.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer1.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            // Peer 2 has different network group.
            IPAddress addressInDifferentNetworkGroup = IPAddress.Parse("::ffff:193.168.0.1");
            var endpointNode2 = new IPEndPoint(addressInDifferentNetworkGroup, 80);
            peerAddressManager.AddPeer(endpointNode2, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer2 = new Mock<INetworkPeer>();
            networkPeer2.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(addressInDifferentNetworkGroup, 80));
            networkPeer2.SetupGet(np => np.RemoteSocketAddress).Returns(addressInDifferentNetworkGroup);
            networkPeer2.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer2.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);
            
            // Peer 3 has same network group.
            IPAddress addressInSameNetworkGroup = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointNode3 = new IPEndPoint(addressInSameNetworkGroup, 80);
            peerAddressManager.AddPeer(endpointNode3, IPAddress.Loopback);

            Mock<INetworkPeer> networkPeer3 = new Mock<INetworkPeer>();
            networkPeer3.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(addressInSameNetworkGroup, 80));
            networkPeer3.SetupGet(np => np.RemoteSocketAddress).Returns(addressInSameNetworkGroup);
            networkPeer3.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer3.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);
            
            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode1);
            connectionManagerSettingsExisting.Connect.Add(endpointNode1);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode2);
            connectionManagerSettingsExisting.Connect.Add(endpointNode2);

            connectionManagerSettingsExisting.AddNode.Add(endpointNode3);
            connectionManagerSettingsExisting.Connect.Add(endpointNode3);
            
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode1)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer1.Object));
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode2)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer2.Object));
            networkPeerFactoryExisting.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode3)), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer3.Object));
           
            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker());

            IConnectionManager connectionManagerExisting = this.CreateConnectionManager(nodeSettings, connectionManagerSettingsExisting, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManagerExisting);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();
            
            // Connects to nodes from a different network group.
            Assert.Contains(endpointNode1, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointNode2, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));

            // Does not connect to nodes in the same network group.
            Assert.DoesNotContain(endpointNode3, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));     
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