using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.P2P
{
    public sealed class PeerConnectionTests
    {
        ILoggerFactory loggerFactory;
        NetworkPeerConnectionParameters parameters;
        NetworkPeerFactory networkPeerFactory;
        INodeLifetime nodeLifetime;
        NodeSettings nodeSettings;
        IPeerAddressManager peerAddressManager;

        [Fact]
        public void PeerConnectorDiscovery_CanConnect_ToDiscoveredPeers()
        {
            CreateTestContext("PeerConnectorDiscovery_Scenario_1");

            var peerConnectorDiscovery = new PeerConnectorDiscovery(
                new AsyncLoopFactory(this.loggerFactory),
                this.loggerFactory,
                Network.StratisMain,
                this.networkPeerFactory,
                this.nodeLifetime,
                this.nodeSettings,
                this.peerAddressManager);

            var peerDiscovery = new PeerDiscovery(
                new AsyncLoopFactory(this.loggerFactory),
                this.loggerFactory,
                Network.StratisMain,
                this.networkPeerFactory,
                this.nodeLifetime,
                this.nodeSettings,
                this.peerAddressManager);

            IConnectionManager connectionManager = new ConnectionManager(
                new AsyncLoopFactory(this.loggerFactory),
                new DateTimeProvider(),
                this.loggerFactory,
                Network.StratisMain,
                this.networkPeerFactory,
                this.nodeSettings,
                this.nodeLifetime,
                this.parameters,
                this.peerAddressManager,
                new IPeerConnector[] { peerConnectorDiscovery },
                peerDiscovery);

            // Start peer discovery.
            peerDiscovery.DiscoverPeers(connectionManager);

            // Wait until we have discovered 10 peers.
            TestHelper.WaitLoop(() => this.peerAddressManager.Peers.Count > 10);

            // Wait until at least one successful connection has been made.
            while (true)
            {
                try
                {
                    using (CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(this.parameters.ConnectCancellation))
                    {
                        var connectParameters = this.parameters.Clone();

                        cancel.CancelAfter((int)TimeSpans.FiveSeconds.TotalMilliseconds);

                        connectParameters.ConnectCancellation = cancel.Token;

                        var peerAddress = this.peerAddressManager.SelectPeerToConnectTo();
                        var networkPeer = this.networkPeerFactory.CreateConnectedNetworkPeer(Network.StratisMain, peerAddress, connectParameters);
                        networkPeer.VersionHandshake();
                        networkPeer.Disconnect();

                        break;
                    }
                }
                catch
                {
                }
            };

            // Stop peer discovery.
            this.nodeLifetime.StopApplication();
            peerDiscovery.Dispose();
        }

        [Fact]
        public void PeerConnectorAddNode_PeerAlreadyConnected_FindPeerToConnectTo_Returns_None()
        {
            CreateTestContext("PeerConnectorAddNode_Scenario_1");

            var peerConnectorAddNode = new PeerConnectorAddNode(new AsyncLoopFactory(this.loggerFactory), this.loggerFactory, Network.StratisMain, this.networkPeerFactory, this.nodeLifetime, this.nodeSettings, this.peerAddressManager);

            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(this.loggerFactory), this.loggerFactory, Network.StratisMain, this.networkPeerFactory, this.nodeLifetime, this.nodeSettings, this.peerAddressManager);

            IConnectionManager connectionManager = new ConnectionManager(
                new AsyncLoopFactory(this.loggerFactory),
                new DateTimeProvider(),
                this.loggerFactory,
                Network.StratisMain,
                this.networkPeerFactory,
                this.nodeSettings,
                this.nodeLifetime,
                this.parameters,
                this.peerAddressManager,
                new IPeerConnector[] { peerConnectorAddNode },
                peerDiscovery);

            // Start peer discovery.
            peerDiscovery.DiscoverPeers(connectionManager);

            // Wait until we have discovered at least 10 peers.
            TestHelper.WaitLoop(() => this.peerAddressManager.Peers.Count > 10);

            // Get 2 peers that have been successfully connected to and handshaked.
            var successfulPeers = new List<NetworkPeer>();
            while (successfulPeers.Count <= 2)
            {
                try
                {
                    using (CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(this.parameters.ConnectCancellation))
                    {
                        var connectParameters = this.parameters.Clone();

                        cancel.CancelAfter((int)TimeSpans.FiveSeconds.TotalMilliseconds);

                        connectParameters.ConnectCancellation = cancel.Token;

                        var peerAddress = this.peerAddressManager.SelectPeerToConnectTo();
                        var successfulPeer = this.networkPeerFactory.CreateConnectedNetworkPeer(Network.StratisMain, peerAddress, connectParameters);
                        successfulPeer.VersionHandshake();
                        successfulPeers.Add(successfulPeer);

                        successfulPeer.Disconnect();
                    }
                }
                catch
                {
                }
            };

            // Stop peer discovery.
            this.nodeLifetime.StopApplication();
            peerDiscovery.Dispose();

            // Re-create the nodeLifetime and clear the address
            // manager's peers.
            this.nodeLifetime = new NodeLifetime();
            this.peerAddressManager.Peers.Clear();

            // Add the successful peers to the connection manager's
            // add node collection.
            foreach (var successfulPeer in successfulPeers)
            {
                connectionManager.AddNodeAddress(successfulPeer.PeerAddress.Endpoint);
            }

            // Add the first successful peer to the already connected
            // peer collection of connection manager.
            //
            // This is to simulate that a peer has successfully connected
            // and that add node connector's Find method then won't 
            // return the added node.
            var alreadyConnectedPeer = successfulPeers.First();
            connectionManager.AddConnectedPeer(alreadyConnectedPeer);

            // Re-initialize the add node peer connector so that it
            // adds the successul addresses to the address manager.
            peerConnectorAddNode.Initialize(connectionManager);

            // The already connected peer should not be returned.
            var peer = peerConnectorAddNode.FindPeerToConnectTo();
            Assert.NotEqual(peer.Endpoint, alreadyConnectedPeer.PeerAddress.Endpoint);
        }

        private void CreateTestContext(string folder)
        {
            this.parameters = new NetworkPeerConnectionParameters();

            var testFolder = TestDirectory.Create(folder);

            this.nodeSettings = new NodeSettings
            {
                DataDir = testFolder.FolderName
            };

            this.nodeSettings.DataFolder = new DataFolder(this.nodeSettings);

            this.peerAddressManager = new PeerAddressManager(this.nodeSettings.DataFolder);
            var peerAddressManagerBehaviour = new PeerAddressManagerBehaviour(new DateTimeProvider(), this.peerAddressManager)
            {
                PeersToDiscover = 10
            };

            this.parameters.TemplateBehaviors.Add(peerAddressManagerBehaviour);

            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.networkPeerFactory = new NetworkPeerFactory(new DateTimeProvider(), this.loggerFactory);
            this.nodeLifetime = new NodeLifetime();
        }
    }
}