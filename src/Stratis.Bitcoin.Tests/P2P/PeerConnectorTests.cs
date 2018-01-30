using System;
using System.IO;
using System.Net;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests : TestBase
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
            this.networkPeerFactory = new NetworkPeerFactory(this.network, DateTimeProvider.Default, this.extendedLoggerFactory);
            this.nodeLifetime = new NodeLifetime();
        }

        [Fact]
        public void PeerConnectorAddNode_FindPeerToConnectTo_Returns_AddNodePeers()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);
            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            connectionSettings.AddNode.Add(networkAddressAddNode.Endpoint);

            // TODO: Once we have an interface on NetworkPeer we can test this properly.
            //var connector = this.CreatePeerConnecterAddNode(nodeSettings, peerAddressManager);
            //connector.OnConnectAsync().GetAwaiter().GetResult();
            //Assert.Contains(networkAddressAddNode, connector.ConnectedPeers.Select(p => p.PeerAddress));
        }

        [Fact]
        public void PeerConnectorAddNode_CanAlwaysStart()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);
            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var connector = this.CreatePeerConnecterAddNode(nodeSettings, connectionSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_FindPeerToConnectTo_Returns_ConnectNodePeers()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            connectionSettings.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = this.CreatePeerConnectorConnectNode(nodeSettings, connectionSettings, peerAddressManager);

            // TODO: Once we have an interface on NetworkPeer we can test this properly.
            //var connector = this.CreatePeerConnecterAddNode(nodeSettings, peerAddressManager);
            //connector.OnConnectAsync().GetAwaiter().GetResult();
            //Assert.Contains(networkAddressConnectNode, connector.ConnectedPeers.Select(p => p.PeerAddress));
        }

        [Fact]
        public void PeerConnectorConnect_WithConnectPeersSpecified_CanStart()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            connectionSettings.Connect.Add(networkAddressConnectNode.Endpoint);

            var connector = this.CreatePeerConnectorConnectNode(nodeSettings, connectionSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorConnect_WithNoConnectPeersSpecified_CanNotStart()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var connector = this.CreatePeerConnectorConnectNode(nodeSettings, connectionSettings, peerAddressManager);
            Assert.False(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscovery_FindPeerToConnectTo_Returns_DiscoveredPeers()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            connectionSettings.AddNode.Add(networkAddressAddNode.Endpoint);
            connectionSettings.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = this.CreatePeerConnectorDiscovery(nodeSettings, connectionSettings, peerAddressManager);

            // TODO: Once we have an interface on NetworkPeer we can test this properly.
            //var connector = this.CreatePeerConnecterAddNode(nodeSettings, peerAddressManager);
            //connector.OnConnectAsync().GetAwaiter().GetResult();
            //Assert.Contains(networkAddressDiscoverNode, connector.ConnectedPeers.Select(p => p.PeerAddress));
        }

        [Fact]
        public void PeerConnectorDiscover_WithNoConnectPeersSpecified_CanStart()
        {
            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var connector = this.CreatePeerConnectorDiscovery(nodeSettings, connectionSettings, peerAddressManager);
            Assert.True(connector.CanStartConnect);
        }

        [Fact]
        public void PeerConnectorDiscover_WithConnectPeersSpecified_CanNotStart()
        {
            var nodeSettings = new NodeSettings(args:new string[] { }, loadConfiguration:true);

            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            connectionSettings.Connect.Add(networkAddressConnectNode.Endpoint);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerConnectorTests"));
            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);

            var connector = this.CreatePeerConnectorDiscovery(nodeSettings, connectionSettings, peerAddressManager);
            Assert.False(connector.CanStartConnect);
        }

        private PeerConnectorAddNode CreatePeerConnecterAddNode(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager)
        {
            var peerConnector = new PeerConnectorAddNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager);
            var connectionManager = CreateConnectionManager(nodeSettings, connectionSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            return peerConnector;
        }

        private PeerConnectorConnectNode CreatePeerConnectorConnectNode(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager)
        {           
            var peerConnector = new PeerConnectorConnectNode(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager);
            var connectionManager = CreateConnectionManager(nodeSettings, connectionSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            return peerConnector;
        }

        private PeerConnectorDiscovery CreatePeerConnectorDiscovery(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager)
        {
            var peerConnector = new PeerConnectorDiscovery(this.asyncLoopFactory, DateTimeProvider.Default, this.extendedLoggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, nodeSettings, connectionSettings, peerAddressManager);
            var connectionManager = CreateConnectionManager(nodeSettings, connectionSettings, peerAddressManager, peerConnector);
            peerConnector.Initialize(connectionManager);
            return peerConnector;
        }

        private IConnectionManager CreateConnectionManager(NodeSettings nodeSettings, ConnectionManagerSettings connectionSettings, IPeerAddressManager peerAddressManager, IPeerConnector peerConnector)
        {
            var connectionManager = new ConnectionManager(
                DateTimeProvider.Default,
                this.loggerFactory,
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