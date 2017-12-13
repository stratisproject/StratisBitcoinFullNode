using System.Net;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests
    {
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly ExtendedLoggerFactory loggerFactory;
        private readonly NetworkPeerFactory networkPeerFactory;
        private readonly NodeLifetime nodeLifeTime;

        public PeerConnectorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory);
            this.networkPeerFactory = new NetworkPeerFactory(DateTimeProvider.Default, this.loggerFactory);
            this.nodeLifeTime = new NodeLifetime();
        }

        [Fact]
        public void PeerConnectorAddNode_FindPeerToConnectTo_Returns_AddNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            nodeSettings.ConnectionManager.AddNode.Add(networkAddressAddNode.Endpoint);

            var connector = CreatePeerConnecterAddNode(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressAddNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnectorAddNode_CanAlwaysStart()
        {
            var peerAddressManager = new PeerAddressManager();

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            var connector = CreatePeerConnecterAddNode(nodeSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_FindPeerToConnectTo_Returns_ConnectNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = CreatePeerConnectorConnectNode(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressConnectNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnectorConnect_WithConnectPeersSpecified_CanStart()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);

            var connector = CreatePeerConnectorConnectNode(nodeSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_WithNoConnectPeersSpecified_CanNotStart()
        {
            var peerAddressManager = new PeerAddressManager();
            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            var connector = CreatePeerConnectorConnectNode(nodeSettings, peerAddressManager);
            Assert.False(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscovery_FindPeerToConnectTo_Returns_DiscoveredPeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            nodeSettings.ConnectionManager.AddNode.Add(networkAddressAddNode.Endpoint);
            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = CreatePeerConnectorDiscovery(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressDiscoverNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnectorDiscover_WithNoConnectPeersSpecified_CanStart()
        {
            var peerAddressManager = new PeerAddressManager();

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            var connector = CreatePeerConnectorDiscovery(nodeSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscover_WithConnectPeersSpecified_CanNotStart()
        {
            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);

            var peerAddressManager = new PeerAddressManager();
            var connector = CreatePeerConnectorDiscovery(nodeSettings, peerAddressManager);
            Assert.False(connector.CanStartConnect);
        }

        private PeerConnectorAddNode CreatePeerConnecterAddNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
        {
            var connector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.loggerFactory, Network.StratisTest, this.networkPeerFactory, this.nodeLifeTime, nodeSettings, peerAddressManager);
            connector.RelatedPeerConnector = new RelatedPeerConnectors();
            return connector;
        }

        private PeerConnectorConnectNode CreatePeerConnectorConnectNode(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
        {
            var connector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.loggerFactory, Network.StratisTest, this.networkPeerFactory, this.nodeLifeTime, nodeSettings, peerAddressManager);
            connector.RelatedPeerConnector = new RelatedPeerConnectors();
            return connector;
        }

        private PeerConnectorDiscovery CreatePeerConnectorDiscovery(NodeSettings nodeSettings, IPeerAddressManager peerAddressManager)
        {
            var connector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.loggerFactory, Network.StratisTest, this.networkPeerFactory, this.nodeLifeTime, nodeSettings, peerAddressManager);
            connector.RelatedPeerConnector = new RelatedPeerConnectors();
            return connector;
        }
    }
}