using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
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

            var addressManager = new PeerAddressManager(nodeSettings.DataFolder);
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(new DateTimeProvider(), addressManager)
            {
                PeersToDiscover = 3
            };

            parameters.TemplateBehaviors.Add(addressManagerBehaviour);

            INetworkPeerFactory networkPeerFactory = new NetworkPeerFactory(DateTimeProvider.Default, new LoggerFactory());
            var peerDiscoveryLoop = new PeerDiscoveryLoop(
                new AsyncLoopFactory(new LoggerFactory()),
                Network.Main,
                parameters,
                new NodeLifetime(),
                addressManager,
                networkPeerFactory);

            peerDiscoveryLoop.DiscoverPeers();

            //Wait until we have discovered 3 peers
            while (addressManager.Peers.Count < 3)
            {
                Task.Delay(TimeSpans.Second).GetAwaiter().GetResult();
            }

            var peerOne = addressManager.SelectPeerToConnectTo(PeerIntroductionType.Discover);
            NetworkPeer node = networkPeerFactory.CreateConnectedNetworkPeer(Network.Main, peerOne, parameters);
            node.VersionHandshake();
            node.Disconnect();

            var peerTwo = addressManager.SelectPeerToConnectTo(PeerIntroductionType.Discover);
            NetworkPeer node2 = networkPeerFactory.CreateConnectedNetworkPeer(Network.Main, peerTwo, parameters);
            node.Disconnect();
        }
    }
}
