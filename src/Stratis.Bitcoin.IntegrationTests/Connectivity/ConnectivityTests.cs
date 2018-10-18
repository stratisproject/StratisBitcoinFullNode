using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Connectivity
{
    public class ConnectivityTests
    {
        private readonly Network Network;

        public ConnectivityTests()
        {
            this.Network = new StratisRegTest();
        }

        [Fact]
        public async Task WhenConnectingWithAddnodeConnectToPeerAndAnyPeersInTheAddressManager()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.Network).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.Network).NotInIBD().Start();
                CoreNode node3 = builder.CreateStratisPosNode(this.Network).NotInIBD().Start();
                CoreNode syncerNode = builder.CreateStratisPosNode(this.Network).NotInIBD().Start();

                // Connects with AddNode inside Connect().
                TestHelper.Connect(node1, syncerNode);

                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);

                var syncerConnectionMgr = syncerNode.FullNode.NodeService<IConnectionManager>();

                var peerAddNodeConnector = syncerConnectionMgr.PeerConnectors.
                    Where(p => p.GetType() == typeof(PeerConnectorAddNode)).First() as PeerConnectorAddNode;

                // Adding these endpoints will add the items to the internal address manager.
                peerAddNodeConnector.ConnectionSettings.AddNode = new List<IPEndPoint>() { node2.Endpoint, node3.Endpoint };
                peerAddNodeConnector.Initialize(syncerConnectionMgr);

                TestHelper.WaitLoop(() => syncerConnectionMgr.ConnectedPeers.Count() == 3);

                node1.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
            }
        }
    }
}
