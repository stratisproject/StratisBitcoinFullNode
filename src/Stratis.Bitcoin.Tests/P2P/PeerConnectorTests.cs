using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests : LogsTestBase
    {
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly ExtendedLoggerFactory extendedLoggerFactory;
        private readonly NodeLifetime nodeLifetime;

        public PeerConnectorTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();

            this.asyncLoopFactory = new AsyncLoopFactory(this.extendedLoggerFactory);

            this.nodeLifetime = new NodeLifetime();
        }

        [Fact]
        public void PeerConnectorAddNode_ConnectsTo_AddNodePeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            IPAddress ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            IPAddress ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressTwo, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            connectionManagerSettings.AddNode.Add(endpointAddNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressOne, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressOne);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector, selfEndpointTracker.Object);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.Contains(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorAddNode_CanAlwaysStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            var nodeSettings = new NodeSettings(this.Network);

            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var connector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnectNode_ConnectsTo_ConnectNodePeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            IPAddress ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressOne, 80);

            IPAddress ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            IPAddress ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);
            connectionManagerSettings.Connect.Add(endpointConnectNode);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(ipAddressConnect, 80));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(ipAddressConnect);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(80);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(), It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector, selfEndpointTracker.Object);
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
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            IPAddress ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointConnectNode = new IPEndPoint(ipAddressThree, 80);

            var nodeSettings = new NodeSettings(this.Network);

            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            connectionSettings.Connect.Add(endpointConnectNode);

            var networkPeerFactory = new Mock<INetworkPeerFactory>();

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_WithNoConnectPeersSpecified_CanNotStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            var nodeSettings = new NodeSettings(this.Network);
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_DiscoveredPeers()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            IPAddress ipAddressAdd = IPAddress.Parse("::ffff:192.168.0.1");
            var endpointAddNode = new IPEndPoint(ipAddressAdd, 80);

            IPAddress ipAddressConnect = IPAddress.Parse("::ffff:192.168.0.2");
            var endpointConnectNode = new IPEndPoint(ipAddressConnect, 80);

            IPAddress ipAddressDiscovered = IPAddress.Parse("::ffff:192.168.0.3");
            var endpointDiscoveredNode = new IPEndPoint(ipAddressDiscovered, 80);

            peerAddressManager.AddPeer(endpointAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(endpointDiscoveredNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering=false" });

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

            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionManagerSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();

            IConnectionManager connectionManager = CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector, selfEndpointTracker.Object);
            peerConnector.Initialize(connectionManager);
            peerConnector.OnConnectAsync().GetAwaiter().GetResult();

            Assert.DoesNotContain(endpointAddNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.DoesNotContain(endpointConnectNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
            Assert.Contains(endpointDiscoveredNode, peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint));
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_NodeInSameNetworkGroup_WithIpRangeFilteringDisabled()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            // IpRangeFiltering enabled by default, disabled explicitly.
            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering=false" });

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();

            Mock<IConnectionManager> connectionManagerExisting = new Mock<IConnectionManager>();

            var networkPeerParameters = new NetworkPeerConnectionParameters();
            networkPeerParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(connectionManagerExisting.Object, this.extendedLoggerFactory));

            connectionManagerExisting.SetupGet(np => np.Parameters).Returns(networkPeerParameters);
            connectionManagerExisting.SetupGet(np => np.ConnectedPeers).Returns(new NetworkPeerCollection());

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            peerConnector.Initialize(connectionManagerExisting.Object);

            //Peer 1.
            IPAddress originalAddressPeer1 = IPAddress.Parse("::ffff:57.48.183.81"); // ipv4
            var endpointPeer1 = new IPEndPoint(originalAddressPeer1, 80);
            peerAddressManager.AddPeer(endpointPeer1, IPAddress.Loopback);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer1, connectionManagerExisting);
            Assert.True(connectedToThisPeer);

            // Peer 2 has different network group to Peer 1.
            IPAddress addressInDifferentNetworkGroupPeer2 = IPAddress.Parse("99be:f5c5:adc2:525c:f6d7:7b30:5336:5a0f"); // ipv6
            var endpointPeer2 = new IPEndPoint(addressInDifferentNetworkGroupPeer2, 80);
            peerAddressManager.AddPeer(endpointPeer2, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer2, connectionManagerExisting);
            Assert.True(connectedToThisPeer); // Different network group: connects.

            // Peer 3 in same network group as Peer 2.
            IPAddress addressInSameNetworkGroupPeer3 = IPAddress.Parse("99be:f5c5:adc2:525c:db45:d36e:ce01:a394"); // ipv6
            var endpointPeer3 = new IPEndPoint(addressInSameNetworkGroupPeer3, 80);
            peerAddressManager.AddPeer(endpointPeer3, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer3, connectionManagerExisting);
            Assert.True(connectedToThisPeer); // Same network group: connects.

            // Peer 4 has different network group to Peer 1.
            IPAddress addressInDifferentNetworkGroupPeer4 = IPAddress.Parse("::ffff:58.48.183.81"); // ipv4
            var endpointPeer4 = new IPEndPoint(addressInDifferentNetworkGroupPeer4, 80);
            peerAddressManager.AddPeer(endpointPeer4, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer4, connectionManagerExisting);
            Assert.True(connectedToThisPeer); // Different network group: connects.

            // Peer 5 has same network group as Peer 1.
            IPAddress addressInSameNetworkGroupPeer5 = IPAddress.Parse("::ffff:57.48.183.82"); // ipv4
            var endpointPeer5 = new IPEndPoint(addressInSameNetworkGroupPeer5, 80);
            peerAddressManager.AddPeer(endpointPeer5, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer5, connectionManagerExisting);
            Assert.True(connectedToThisPeer); // Same network group: connects.
        }

        [Fact]
        public void PeerConnectorDiscovery_DoesNotConnectTo_NodeInSameNetworkGroup_WithIpRangeFilteringEnabled_IPv4()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            // IpRangeFiltering enabled by default.
            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering" });

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();
            var connectionManagerExisting = new Mock<IConnectionManager>();

            var networkPeerParameters = new NetworkPeerConnectionParameters();
            networkPeerParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(connectionManagerExisting.Object, this.extendedLoggerFactory));

            connectionManagerExisting.SetupGet(np => np.Parameters).Returns(networkPeerParameters);
            connectionManagerExisting.SetupGet(np => np.ConnectedPeers).Returns(new NetworkPeerCollection());

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            peerConnector.Initialize(connectionManagerExisting.Object);

            // Peer 1.
            IPAddress ipAddressOne = IPAddress.Parse("::ffff:57.48.183.81"); // ipv4
            var endpointPeer1 = new IPEndPoint(ipAddressOne, 80);
            peerAddressManager.AddPeer(endpointPeer1, IPAddress.Loopback);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer1, connectionManagerExisting);
            Assert.True(connectedToThisPeer);

            // Peer 2 has same network group as Peer 1.
            IPAddress ipAddressTwo = IPAddress.Parse("::ffff:57.48.183.82"); // ipv4
            var endpointPeerTwo = new IPEndPoint(ipAddressTwo, 80);
            peerAddressManager.AddPeer(endpointPeerTwo, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeerTwo, connectionManagerExisting);
            Assert.False(connectedToThisPeer); // Same network group: does not connect.
        }

        [Fact]
        public void PeerConnectorDiscovery_DoesNotConnectTo_NodeInSameNetworkGroup_WithIpRangeFilteringEnabled_IPv6()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            // IpRangeFiltering enabled by default.
            var nodeSettings = new NodeSettings(this.Network, args: new[] { "-IpRangeFiltering" });

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();
            var connectionManagerExisting = new Mock<IConnectionManager>();

            var networkPeerParameters = new NetworkPeerConnectionParameters();
            networkPeerParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(connectionManagerExisting.Object, this.extendedLoggerFactory));

            connectionManagerExisting.SetupGet(np => np.Parameters).Returns(networkPeerParameters);
            connectionManagerExisting.SetupGet(np => np.ConnectedPeers).Returns(new NetworkPeerCollection());

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            peerConnector.Initialize(connectionManagerExisting.Object);

            // Peer 1 has different network group to Peer 1.
            IPAddress ipAddressOne = IPAddress.Parse("99be:f5c5:adc2:525c:f6d7:7b30:5336:5a0f"); // ipv6
            var endpointPeerOne = new IPEndPoint(ipAddressOne, 80);
            peerAddressManager.AddPeer(endpointPeerOne, IPAddress.Loopback);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeerOne, connectionManagerExisting);
            Assert.True(connectedToThisPeer); // Different network group: connects.

            // Peer 1 in same network group as Peer 2.
            IPAddress ipAddressTwo = IPAddress.Parse("99be:f5c5:adc2:525c:db45:d36e:ce01:a394"); // ipv6
            var endpointPeerTwo = new IPEndPoint(ipAddressTwo, 80);
            peerAddressManager.AddPeer(endpointPeerTwo, IPAddress.Loopback);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeerTwo, connectionManagerExisting);
            Assert.False(connectedToThisPeer); // Same network group: does not connect.
        }

        [Fact]
        public void PeerConnectorDiscovery_ConnectsTo_LocalNodes_IpRangeFilteringEnabled()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            var nodeSettings = new NodeSettings(this.Network, args: new[] { string.Empty }); // Ip range filtering enabled by default.

            var connectionManagerSettingsExisting = new ConnectionManagerSettings(nodeSettings);
            Mock<INetworkPeerFactory> networkPeerFactoryExisting = new Mock<INetworkPeerFactory>();

            Mock<IConnectionManager> connectionManagerExisting = new Mock<IConnectionManager>();

            var networkPeerParameters = new NetworkPeerConnectionParameters();
            networkPeerParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(connectionManagerExisting.Object, this.extendedLoggerFactory));

            connectionManagerExisting.SetupGet(np => np.Parameters).Returns(networkPeerParameters);
            connectionManagerExisting.SetupGet(np => np.ConnectedPeers).Returns(new NetworkPeerCollection());

            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactoryExisting.Object, this.nodeLifetime, nodeSettings, connectionManagerSettingsExisting, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));

            peerConnector.Initialize(connectionManagerExisting.Object);

            // Peer 1 is a local address.
            IPAddress originalAddressPeer1 = IPAddress.Parse("::ffff:192.168.0.1"); // ipv4
            var endpointPeer1 = new IPEndPoint(originalAddressPeer1, 80);
            bool connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer1, connectionManagerExisting);
            Assert.True(connectedToThisPeer);

            // Peer 2 is a local address in a different group.
            IPAddress addressInDifferentNetworkGroupPeer2 = IPAddress.Parse("::ffff:192.168.1.1"); // ipv4
            var endpointPeer2 = new IPEndPoint(addressInDifferentNetworkGroupPeer2, 80);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer2, connectionManagerExisting);
            Assert.True(connectedToThisPeer);

            // Peer 3 is a local address in a different group.
            IPAddress addressInDifferentNetworkGroupPeer3 = IPAddress.Parse("0:0:0:0:0:ffff:c0a8:101"); // ipv6
            var endpointPeer3 = new IPEndPoint(addressInDifferentNetworkGroupPeer3, 80);
            connectedToThisPeer = ConnectToPeer(peerAddressManager, networkPeerFactoryExisting, connectionManagerSettingsExisting, peerConnector, endpointPeer3, connectionManagerExisting);
            Assert.True(connectedToThisPeer);
        }

        [Fact]
        public void PeerConnectorDiscover_WithNoConnectPeersSpecified_CanStart()
        {
            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            var nodeSettings = new NodeSettings(this.Network);
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));
            Assert.True(peerConnector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscover_WithConnectPeersSpecified_CanNotStart()
        {
            var nodeSettings = new NodeSettings(this.Network);
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new IPEndPoint(ipAddressThree, 80);

            connectionSettings.Connect.Add(networkAddressConnectNode);

            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, new SelfEndpointTracker(this.extendedLoggerFactory));
            Assert.False(peerConnector.CanStartConnect);
        }

        [Fact]
        public void ConnectAsync_WithASelfConnectionAttempt_DoesNotAttemptToConnect()
        {
            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();
            selfEndpointTracker.Setup(x => x.IsSelf(It.IsAny<IPEndPoint>())).Returns(true);
            var peerAddressManager = new Mock<IPeerAddressManager>();
            var nodeSettings = new NodeSettings(this.Network);
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.Network, networkPeerFactory.Object, this.nodeLifetime, nodeSettings, new ConnectionManagerSettings(nodeSettings), peerAddressManager.Object, selfEndpointTracker.Object);

            peerConnector.ConnectAsync(new PeerAddress()).GetAwaiter().GetResult();

            peerAddressManager.Verify(x => x.PeerAttempted(It.IsAny<IPEndPoint>(), It.IsAny<DateTime>()), Times.Never());
        }

        [Fact]
        public void ConnectionManager_AddsExternalIpToSelfEndpointTracker()
        {
            const string externalIp = "8.8.8.8";

            DataFolder peerFolder = CreateDataFolder(this);
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));

            var nodeSettings = new NodeSettings(this.Network, args: new[] { $"-externalip={externalIp}" });

            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings)
            {
                Bind = new List<NodeServerEndpoint>()
            };

            var networkPeer = new Mock<INetworkPeer>();
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerConnector = new Mock<IPeerConnector>();
            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();
            IConnectionManager connectionManager = this.CreateConnectionManager(nodeSettings, connectionManagerSettings, peerAddressManager, peerConnector.Object, selfEndpointTracker.Object);

            connectionManager.Initialize(new Mock<IConsensusManager>().Object);

            selfEndpointTracker.Verify(x => x.Add(new IPEndPoint(IPAddress.Parse(externalIp), this.Network.DefaultPort)), Times.Once);
        }

        [Fact]
        public void Ensure_IPGroups_Are_The_Same()
        {
            var ipAddress100 = IPAddress.Parse("::ffff:100.50.0.3");
            var endPoint100 = new IPEndPoint(ipAddress100, 80);

            var ipAddress100_2 = IPAddress.Parse("::ffff:100.50.0.3");
            var endPoint100_2 = new IPEndPoint(ipAddress100_2, 80);

            byte[] endPoint100Group = endPoint100.MapToIpv6().Address.GetGroup();
            byte[] endPoint100_2Group = endPoint100_2.MapToIpv6().Address.GetGroup();

            Assert.True(endPoint100_2Group.SequenceEqual(endPoint100Group));
        }

        [Fact]
        public void Ensure_IPGroups_Are_Filtered()
        {
            var ipAddress100 = IPAddress.Parse("::ffff:100.50.0.3");
            var endPoint100 = new IPEndPoint(ipAddress100, 80);

            var ipAddress100_2 = IPAddress.Parse("::ffff:100.67.0.3");
            var endPoint100_2 = new IPEndPoint(ipAddress100_2, 80);

            byte[] endPoint100Group = endPoint100.MapToIpv6().Address.GetGroup();
            byte[] endPoint100_2Group = endPoint100_2.MapToIpv6().Address.GetGroup();

            Assert.False(endPoint100_2Group.SequenceEqual(endPoint100Group));
        }

        private IConnectionManager CreateConnectionManager(
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            IPeerConnector peerConnector,
            ISelfEndpointTracker selfEndpointTracker)
        {
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            var peerDiscovery = new Mock<IPeerDiscovery>();

            var networkPeerParameters = new NetworkPeerConnectionParameters();

            var connectionManager = new ConnectionManager(
                DateTimeProvider.Default,
                this.LoggerFactory.Object,
                this.Network,
                networkPeerFactory.Object,
                nodeSettings,
                this.nodeLifetime,
                networkPeerParameters,
                peerAddressManager,
                new IPeerConnector[] { peerConnector },
                peerDiscovery.Object,
                selfEndpointTracker,
                connectionSettings,
                new VersionProvider(),
                new Mock<INodeStats>().Object);

            networkPeerParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(connectionManager, this.extendedLoggerFactory));

            return connectionManager;
        }

        private bool ConnectToPeer(PeerAddressManager peerAddressManager, Mock<INetworkPeerFactory> networkPeerFactoryExisting, ConnectionManagerSettings connectionManagerSettingsExisting, PeerConnector peerConnector, IPEndPoint endpointNode, Mock<IConnectionManager> mockConnectionManager)
        {
            peerAddressManager.AddPeer(endpointNode, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(np => np.PeerEndPoint).Returns(new IPEndPoint(endpointNode.Address, endpointNode.Port));
            networkPeer.SetupGet(np => np.RemoteSocketAddress).Returns(endpointNode.Address);
            networkPeer.SetupGet(np => np.RemoteSocketPort).Returns(endpointNode.Port);
            networkPeer.SetupGet(np => np.State).Returns(NetworkPeerState.HandShaked);
            networkPeerFactoryExisting.Setup(npf =>
                npf.CreateConnectedNetworkPeerAsync(It.Is<IPEndPoint>(x => Equals(x, endpointNode)),
                    It.IsAny<NetworkPeerConnectionParameters>(), It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(networkPeer.Object));

            var connectedPeers = (NetworkPeerCollection)mockConnectionManager.Object.ConnectedPeers;
            foreach (INetworkPeer peer in peerConnector.ConnectorPeers)
            {
                if (!connectedPeers.Contains(peer))
                {
                    connectedPeers.Add(peer);
                }
            }

            mockConnectionManager.SetupGet(np => np.ConnectedPeers).Returns(connectedPeers);
            peerConnector.Initialize(mockConnectionManager.Object);

            peerConnector.OnConnectAsync().GetAwaiter().GetResult();
            return peerConnector.ConnectorPeers.Select(p => p.PeerEndPoint).Contains(endpointNode);
        }
    }
}