using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.P2P
{
    public sealed class PeerConnectionTests
    {
        private ILoggerFactory loggerFactory;
        private NetworkPeerConnectionParameters parameters;
        private NetworkPeerFactory networkPeerFactory;
        private INodeLifetime nodeLifetime;
        private NodeSettings nodeSettings;
        private ConnectionManagerSettings connectionSetting;
        private IPeerAddressManager peerAddressManager;
        private readonly Network network;

        public PeerConnectionTests()
        {
            this.network = Network.Main;
        }

        /// <summary>
        /// This tests the fact that we can't find and connect to a
        /// peer that is already connected.
        /// </summary>
        [Fact]
        public void PeerConnectorAddNode_PeerAlreadyConnected_Scenario1()
        {
            this.CreateTestContext("PeerConnectorAddNode_PeerAlreadyConnected_Scenario1");

            var peerConnectorAddNode = new PeerConnectorAddNode(new AsyncLoopFactory(this.loggerFactory), DateTimeProvider.Default, this.loggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, this.nodeSettings, this.connectionSetting, this.peerAddressManager);
            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(this.loggerFactory), this.loggerFactory, this.network, this.networkPeerFactory, this.nodeLifetime, this.nodeSettings, this.peerAddressManager);
            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(this.nodeSettings);

            IConnectionManager connectionManager = new ConnectionManager(
                DateTimeProvider.Default,
                this.loggerFactory,
                this.network,
                this.networkPeerFactory,
                this.nodeSettings,
                this.nodeLifetime,
                this.parameters,
                this.peerAddressManager,
                new IPeerConnector[] { peerConnectorAddNode },
                peerDiscovery,
                connectionSettings);

            // Create a peer to add to the already connected peers collection.
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode coreNode = builder.CreateStratisPowNode();
                builder.StartAll();

                using (NetworkPeer networkPeer = coreNode.CreateNetworkPeerClient())
                {
                    // Add the network peers to the connection manager's
                    // add node collection.
                    connectionManager.AddNodeAddress(networkPeer.PeerEndPoint);

                    // Add the peer to the already connected
                    // peer collection of connection manager.
                    //
                    // This is to simulate that a peer has successfully connected
                    // and that the add node connector's Find method then won't
                    // return the added node.
                    connectionManager.AddConnectedPeer(networkPeer);

                    // Re-initialize the add node peer connector so that it
                    // adds the successful address to the address manager.
                    peerConnectorAddNode.Initialize(connectionManager);

                    // TODO: Once we have an interface on NetworkPeer we can test this properly.
                    // The already connected peer should not be returned.
                    //var peer = peerConnectorAddNode.FindPeerToConnectTo();
                    //Assert.Null(peer);
                }
            }
        }

        private void CreateTestContext(string folder)
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.parameters = new NetworkPeerConnectionParameters();

            var testFolder = TestDirectory.Create(folder);

            this.nodeSettings = new NodeSettings(args:new string[] { $"-datadir={testFolder.FolderName}" }, loadConfiguration:true);
            this.nodeSettings.DataFolder = new DataFolder(this.nodeSettings.DataDir);

            this.connectionSetting = new ConnectionManagerSettings();
            this.connectionSetting.Load(this.nodeSettings);

            this.peerAddressManager = new PeerAddressManager(this.nodeSettings.DataFolder, this.loggerFactory);
            var peerAddressManagerBehaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, this.peerAddressManager)
            {
                PeersToDiscover = 10
            };

            this.parameters.TemplateBehaviors.Add(peerAddressManagerBehaviour);

            this.networkPeerFactory = new NetworkPeerFactory(this.network, DateTimeProvider.Default, this.loggerFactory);
            this.nodeLifetime = new NodeLifetime();
        }
    }
}