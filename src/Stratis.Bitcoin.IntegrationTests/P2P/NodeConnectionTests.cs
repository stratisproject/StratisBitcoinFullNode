using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
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

            var nodeSettings = new NodeSettings
            {
                DataDir = testFolder.FolderName
            };

            nodeSettings.DataFolder = new DataFolder(nodeSettings);

            var addressManager = new PeerAddressManager(nodeSettings.DataFolder);
            var addressManagerBehaviour = new PeerAddressManagerBehaviour(addressManager)
            {
                Mode = PeerAddressManagerBehaviourMode.Discover,
                PeersToDiscover = 5
            };

            parameters.TemplateBehaviors.Add(addressManagerBehaviour);

            var peerDiscoveryLoop = new PeerDiscoveryLoop(Network.Main, new AsyncLoopFactory(new LoggerFactory()), parameters, new System.Threading.CancellationToken());
            peerDiscoveryLoop.Start();

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
        }
    }
}