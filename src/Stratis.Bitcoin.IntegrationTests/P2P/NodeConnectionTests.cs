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

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            nodeSettings.DataFolder = new DataFolder(nodeSettings);

            var peerAddressManager = new PeerAddressManager(nodeSettings.DataFolder, loggerFactory);
            var peerAddressManagerBehaviour = new PeerAddressManagerBehaviour(new DateTimeProvider(), peerAddressManager)
            {
                PeersToDiscover = 3
            };

            parameters.TemplateBehaviors.Add(peerAddressManagerBehaviour);

            var networkPeerFactory = new NetworkPeerFactory(DateTimeProvider.Default, loggerFactory);

            var peerConnectorDiscovery = new PeerConnectorDiscovery(
                new AsyncLoopFactory(loggerFactory),
                loggerFactory,
                Network.Main,
                networkPeerFactory,
                new NodeLifetime(),
                nodeSettings,
                peerAddressManager);

            var peerDiscovery = new PeerDiscovery(
                new AsyncLoopFactory(loggerFactory),
                loggerFactory,
                Network.Main,
                networkPeerFactory,
                new NodeLifetime(),
                nodeSettings,
                peerAddressManager);

            IConnectionManager connectionManager = new ConnectionManager(
                new AsyncLoopFactory(loggerFactory),
                new DateTimeProvider(),
                loggerFactory,
                Network.Main,
                networkPeerFactory,
                nodeSettings,
                new NodeLifetime(),
                parameters,
                peerAddressManager,
                new IPeerConnector[] { peerConnectorDiscovery },
                peerDiscovery);

            NetworkPeerConnectionParameters cloned = parameters.Clone();
            cloned.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, connectionManager, loggerFactory));

            peerDiscovery.DiscoverPeers(cloned);

            // Wait until we have discovered 3 peers.
            TestHelper.WaitLoop(() => peerAddressManager.Peers.Count > 3);

            // Wait until at least one successful connection has been made.
            while (true)
            {
                try
                {
                    var peerAddress = peerAddressManager.Selector.SelectPeer();
                    NetworkPeer peer = networkPeerFactory.CreateConnectedNetworkPeer(Network.Main, peerAddress.NetworkAddress, parameters);
                    peer.VersionHandshake();
                    peer.Disconnect();

                    break;
                }
                catch
                {
                }
            };
        }
    }
}