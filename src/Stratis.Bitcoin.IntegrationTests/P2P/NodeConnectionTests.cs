using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.P2P
{
    public sealed class NodeConnectionTests
    {
        [Fact]
        public void CanConnectToRandomNode()
        {
            var parameters = new NodeConnectionParameters();

            var testFolder = TestDirectory.Create("CanConnectToRandomNode");
            var addressManager = new PeerAddressManager(new AsyncLoopFactory(new LoggerFactory()), testFolder.FolderName);
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(addressManager)
            {
                Mode = PeerAddressManagerBehaviourMode.Discover,
                PeersToDiscover = 5
            };

            parameters.TemplateBehaviors.Add(addressManagerBehaviour);
            parameters.PeerAddressManagerBehaviour().DiscoverPeers(Network.Main, parameters);

            //Wait until we have discovered 5 peers
            while (addressManager.Peers.Count < 5)
            {
                Task.Delay(TimeSpans.Second);
            }

            var peerOne = addressManager.SelectPeerToConnectTo();
            Node node = Node.Connect(Network.Main, peerOne, parameters);
            node.VersionHandshake();
            node.Disconnect();

            var peerTwo = addressManager.SelectPeerToConnectTo();
            Node node2 = Node.Connect(Network.Main, peerTwo, parameters);
            node.Disconnect();

            addressManager.SavePeers();
        }
    }
}