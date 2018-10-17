using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Connectivity
{
    public class ConnectivityTests
    {
        private readonly Network Network;

        public ConnectivityTests()
        {
            this.Network = KnownNetworks.StratisRegTest;
        }

        [Fact]
        public async Task WhenConnectingWithAddnodeConnectToPeerAndAnyPeersInTheAddressManager()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.Network).NotInIBD();
                CoreNode node2 = builder.CreateStratisPosNode(this.Network).NotInIBD();
                CoreNode node3 = builder.CreateStratisPosNode(this.Network).NotInIBD();

                CoreNode syncerNode = builder.CreateStratisPosNode(this.Network).NotInIBD();

                builder.StartAll();

                // Connects with AddNode inside Connect().
                TestHelper.Connect(node1, syncerNode);

                node1.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                syncerNode.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);

                var syncerConnectionMgr = syncerNode.FullNode.NodeService<IConnectionManager>();

                var peerAddNodeConnector = syncerConnectionMgr.PeerConnectors.
                    Where(p => p.GetType() == typeof(PeerConnectorAddNode)).First() as PeerConnectorAddNode;

                peerAddNodeConnector.Should().NotBeNull();
                peerAddNodeConnector.Should().BeOfType<PeerConnectorAddNode>();

                // Adding these endpoints will add the items to the internal address manager.
                peerAddNodeConnector.ConnectionSettings.AddNode = new List<IPEndPoint>() { node2.Endpoint, node3.Endpoint };
                peerAddNodeConnector.Initialize(syncerConnectionMgr);
                await peerAddNodeConnector.OnConnectAsync();

                node1.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(1);
                syncerConnectionMgr.ConnectedPeers.Count().Should().Be(3);
            }
        }
    }
}
