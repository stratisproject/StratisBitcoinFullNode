using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Connectivity
{
    public class ConnectivityTests
    {
        private readonly Network posNetwork;
        private readonly Network powNetwork;

        public ConnectivityTests()
        {
            this.posNetwork = new StratisRegTest();
            this.powNetwork = new BitcoinRegTest();
        }

        /// <summary>
        /// Peer A_1 connects to Peer A_2
        /// Peer B_1 connects to Peer B_2
        /// Peer A_1 connects to Peer B_1
        ///
        /// Peer A_1 asks Peer B_1 for its addresses and gets Peer B_2
        /// Peer A_1 now also connects to Peer B_2
        /// </summary>
        [Fact]
        [Trait("Unstable", "True")]
        public void Ensure_Peer_CanDiscover_Address_From_ConnectedPeers_And_Connect_ToThem()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode nodeGroupA_1 = builder.CreateStratisPowNode(this.powNetwork, "nodeGroupA_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_1 = builder.CreateStratisPowNode(this.powNetwork, "nodeGroupB_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_2 = builder.CreateStratisPowNode(this.powNetwork, "nodeGroupB_2").EnablePeerDiscovery().Start();

                // Connect group 2 nodes.
                TestHelper.WaitLoop(() => nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().Peers.Count == 0);
                nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeGroupB_2.Endpoint, IPAddress.Loopback);
                TestHelper.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupB_1, nodeGroupB_2));

                // Connect group 1 to group 2
                // This will add all nodeGroupB_1's addresses which includes nodeGroupB_2 to nodeGroupA_1
                nodeGroupA_1.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeGroupB_1.Endpoint, IPAddress.Loopback);
                TestHelper.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupA_1, nodeGroupB_1));

                // First check that B_2 now has more than the initial connection to B_1
                TestHelper.WaitLoop(() => nodeGroupB_2.FullNode.ConnectionManager.ConnectedPeers.Count() > 1);

                // Ensure that B_2 got connected to via either A_1 or A_2
                TestHelper.WaitLoop(() =>
                {
                    if (TestHelper.IsNodeConnectedTo(nodeGroupA_1, nodeGroupB_2))
                        return true;
                    return false;
                });
            }
        }

        [Fact]
        public void When_Connecting_WithAddnode_Connect_ToPeer_AndAnyPeers_InTheAddressManager()
        {
            // TS101_Connectivity_CallAddNode.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork).Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork).Start();
                CoreNode node3 = builder.CreateStratisPosNode(this.posNetwork).Start();
                CoreNode syncerNode = builder.CreateStratisPosNode(this.posNetwork).Start();

                TestHelper.Connect(node1, syncerNode);

                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);

                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node2.Endpoint);
                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node3.Endpoint);

                TestHelper.WaitLoop(() => syncerNode.FullNode.ConnectionManager.ConnectedPeers.Count() == 3);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node2.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node3.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
            }
        }

        [Fact]
        public void When_Connecting_WithConnectOnly_Connect_ToTheRequestedPeer()
        {
            // TS102_Connectivity_CallConnect.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork).Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork).Start();

                var node2ConnectionMgr = node2.FullNode.NodeService<IConnectionManager>();

                var node2PeerNodeConnector = node2ConnectionMgr.PeerConnectors.
                    Where(p => p.GetType() == typeof(PeerConnectorConnectNode)).First() as PeerConnectorConnectNode;

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();

                node2PeerNodeConnector.ConnectionSettings.Connect = new List<IPEndPoint>() { node1.Endpoint };

                node2ConnectionMgr.Initialize(node2.FullNode.NodeService<IConsensusManager>());

                TestHelper.WaitLoop(() => node2ConnectionMgr.ConnectedPeers.Count() == 1);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
            }
        }

        [Fact]
        public void BannedNode_Tries_ToConnect_ItFails_ToEstablishConnection()
        {
            // TS105_Connectivity_PreventConnectingToBannedNodes.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork).Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork).Start();

                node1 = BanNode(node1, node2);

                // Here we have to use the RPC client directly so that we can get the exception.
                Action connectAction = () => node1.CreateRPCClient().AddNode(node2.Endpoint, true);
                connectAction.Should().Throw<RPCException>().WithMessage("Internal error");

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();

                node1 = RemoveBan(node1, node2);

                TestHelper.Connect(node1, node2);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void Not_Fail_IfTry_ToConnect_ToNonExisting_NodeAsync()
        {
            // TS106_Connectivity_CanErrorHandleConnectionToNonExistingNodes.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork).Start();

                var node1ConnectionMgr = node1.FullNode.NodeService<IConnectionManager>();

                var node1PeerNodeConnector = node1ConnectionMgr.PeerConnectors.Where(p => p.GetType() == typeof(PeerConnectorConnectNode)).First() as PeerConnectorConnectNode;

                var nonExistentEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 90);
                node1PeerNodeConnector.ConnectionSettings.Connect = new List<IPEndPoint>() { nonExistentEndpoint };

                node1ConnectionMgr.Initialize(node1.FullNode.NodeService<IConsensusManager>());

                node1PeerNodeConnector.OnConnectAsync().GetAwaiter().GetResult();

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();
            }
        }

        private CoreNode BanNode(CoreNode sourceNode, CoreNode nodeToBan)
        {
            sourceNode.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeToBan.Endpoint, IPAddress.Loopback);

            PeerAddress peerAddress = sourceNode.FullNode.NodeService<IPeerAddressManager>().Peers.FirstOrDefault();

            if (peerAddress != null)
                peerAddress.BanUntil = DateTime.UtcNow.AddMinutes(1);

            return sourceNode;
        }

        private CoreNode RemoveBan(CoreNode sourceNode, CoreNode bannedNode)
        {
            sourceNode.FullNode.NodeService<IPeerAddressManager>().AddPeer(bannedNode.Endpoint, IPAddress.Loopback);

            PeerAddress peerAddress = sourceNode.FullNode.NodeService<IPeerAddressManager>().Peers.FirstOrDefault();

            if (peerAddress != null)
                peerAddress.BanUntil = null;

            return sourceNode;
        }
    }
}
