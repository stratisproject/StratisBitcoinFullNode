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
    public sealed class NodeConnectionTests
    {
        [Fact]
        public void CanDiscoverAndConnectToPeersOnTheNetwork()
        {
            var parameters = new NetworkPeerConnectionParameters();

            var testFolder = TestDirectory.Create("CanDiscoverAndConnectToPeersOnTheNetwork");

            var nodeSettings = new NodeSettings
            {
                DataDir = testFolder.FolderName
            };

            nodeSettings.DataFolder = new DataFolder(nodeSettings);

            var peerAddressManager = new PeerAddressManager(nodeSettings.DataFolder);
            var peerAddressManagerBehaviour = new PeerAddressManagerBehaviour(new DateTimeProvider(), peerAddressManager)
            {
                PeersToDiscover = 3
            };

            parameters.TemplateBehaviors.Add(peerAddressManagerBehaviour);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            INetworkPeerFactory networkPeerFactory = new NetworkPeerFactory(DateTimeProvider.Default, loggerFactory);

            ConnectionManager connectionManager = new ConnectionManager(
                Network.Main, parameters, nodeSettings, loggerFactory, new NodeLifetime(),
                new AsyncLoopFactory(loggerFactory), peerAddressManager, new DateTimeProvider(),
                networkPeerFactory, new IPeerConnector[] { new PeerConnectorDiscovery() });

            var peerDiscoveryLoop = new PeerDiscoveryLoop(connectionManager);
            peerDiscoveryLoop.DiscoverPeers();

            // Wait until we have discovered 3 peers.
            TestHelper.WaitLoop(() => peerAddressManager.Peers.Count > 3);

            // Wait until at least one successful connection has been made.
            while (true)
            {
                try
                {
                    var peerOne = peerAddressManager.SelectPeerToConnectTo();
                    NetworkPeer node = networkPeerFactory.CreateConnectedNetworkPeer(Network.Main, peerOne, parameters);
                    node.VersionHandshake();
                    node.Disconnect();

                    break;
                }
                catch
                {
                }
            };
        }
    }
}