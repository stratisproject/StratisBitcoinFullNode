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
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.Extensions;
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

        [Fact]
        public void Ensure_Node_DoesNot_ReconnectTo_SameNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var nodeConfig = new NodeConfigParameters
                {
                    { "-debug", "1" }
                };

                CoreNode nodeA = builder.CreateStratisPowNode(this.powNetwork, "conn-1-nodeA", configParameters: nodeConfig).Start();
                CoreNode nodeB = builder.CreateStratisPowNode(this.powNetwork, "conn-1-nodeB", configParameters: nodeConfig).Start();

                TestHelper.Connect(nodeA, nodeB);
                TestHelper.ConnectNoCheck(nodeA, nodeB);

                TestBase.WaitLoop(() => nodeA.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);
                TestBase.WaitLoop(() => nodeB.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);

                Assert.False(nodeA.FullNode.ConnectionManager.ConnectedPeers.First().Inbound);
                Assert.True(nodeB.FullNode.ConnectionManager.ConnectedPeers.First().Inbound);
            }
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
        public void Ensure_Peer_CanDiscover_Address_From_ConnectedPeers_And_Connect_ToThem()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode nodeGroupA_1 = builder.CreateStratisPowNode(this.powNetwork, "conn-2-nodeGroupA_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_1 = builder.CreateStratisPowNode(this.powNetwork, "conn-2-nodeGroupB_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_2 = builder.CreateStratisPowNode(this.powNetwork, "conn-2-nodeGroupB_2").EnablePeerDiscovery().Start();

                // Connect B_1 to B_2.
                nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeGroupB_2.Endpoint, IPAddress.Loopback);
                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupB_1, nodeGroupB_2));
                TestBase.WaitLoop(() =>
                {
                    return nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_2.Endpoint));
                });

                // Connect group A_1 to B_1
                // A_1 will receive B_1's addresses which includes B_2.
                TestHelper.Connect(nodeGroupA_1, nodeGroupB_1);

                //Wait until A_1 contains both B_1 and B_2's addresses in its address manager.
                TestBase.WaitLoop(() =>
                 {
                     var result = nodeGroupA_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_1.Endpoint));
                     if (result)
                         return nodeGroupA_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_2.Endpoint));
                     return false;
                 });

                // Wait until A_1 connected to B_2.
                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupA_1, nodeGroupB_2));
            }
        }

        [Fact]
        public void When_Connecting_WithAddnode_Connect_ToPeer_AndAnyPeers_InTheAddressManager()
        {
            // TS101_Connectivity_CallAddNode.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork, "conn-3-node1").Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork, "conn-3-node2").Start();
                CoreNode node3 = builder.CreateStratisPosNode(this.posNetwork, "conn-3-node3").Start();
                CoreNode syncerNode = builder.CreateStratisPosNode(this.posNetwork, "conn-3-syncerNode").Start();

                TestHelper.Connect(node1, syncerNode);

                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);

                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node2.Endpoint);
                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node3.Endpoint);

                TestBase.WaitLoop(() => syncerNode.FullNode.ConnectionManager.ConnectedPeers.Count() == 3);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node2.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node3.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
            }
        }

        [Fact]
        public void When_Connecting_WithConnectOnly_Connect_ToTheRequestedPeer()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork, "conn-4-node1").Start();

                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", node1.Endpoint.ToString() }
                };

                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork, "conn-4-node2", configParameters: nodeConfig).Start();

                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(node1, node2));
            }
        }

        [Fact]
        public void BannedNode_Tries_ToConnect_ItFails_ToEstablishConnection()
        {
            // TS105_Connectivity_PreventConnectingToBannedNodes.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork, "conn-5-node1").Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.posNetwork, "conn-5-node2").Start();

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
                CoreNode node1 = builder.CreateStratisPosNode(this.posNetwork, "conn-6-node1").Start();

                var node1ConnectionMgr = node1.FullNode.NodeService<IConnectionManager>();

                var node1PeerNodeConnector = node1ConnectionMgr.PeerConnectors.Where(p => p.GetType() == typeof(PeerConnectorConnectNode)).First() as PeerConnectorConnectNode;

                var nonExistentEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 90);
                node1PeerNodeConnector.ConnectionSettings.Connect = new List<IPEndPoint>() { nonExistentEndpoint };

                node1ConnectionMgr.Initialize(node1.FullNode.NodeService<IConsensusManager>());

                node1PeerNodeConnector.OnConnectAsync().GetAwaiter().GetResult();

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();
            }
        }

        [Fact]
        public void NodeServer_Disabled_When_ConnectNode_Args_Specified()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", "0" }
                };

                CoreNode node1 = builder.CreateStratisPowNode(this.powNetwork, "conn-7-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.powNetwork, "conn-7-node2").Start();

                Assert.False(node1.FullNode.ConnectionManager.Servers.Any());

                try
                {
                    // Manually call AddNode so that we can catch the exception.
                    node2.CreateRPCClient().AddNode(node1.Endpoint, true);
                }
                catch (Exception ex)
                {
                    Assert.IsType<RPCException>(ex);
                }

                Assert.False(TestHelper.IsNodeConnectedTo(node2, node1));
            }
        }

        [Fact]
        public void NodeServer_Disabled_When_Listen_Specified_AsFalse()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var nodeConfig = new NodeConfigParameters
                {
                    { "-listen", "0" }
                };

                CoreNode node1 = builder.CreateStratisPowNode(this.powNetwork, "conn-8-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.powNetwork, "conn-8-node2").Start();

                Assert.False(node1.FullNode.ConnectionManager.Servers.Any());

                try
                {
                    // Manually call AddNode so that we can catch the exception.
                    node2.CreateRPCClient().AddNode(node1.Endpoint, true);
                }
                catch (Exception ex)
                {
                    Assert.IsType<RPCException>(ex);
                }

                Assert.False(TestHelper.IsNodeConnectedTo(node2, node1));
            }
        }

        [Fact]
        public void NodeServer_Enabled_When_ConnectNode_Args_Specified_And_Listen_Specified()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", "0" },
                    { "-listen", "1" }
                };

                CoreNode node1 = builder.CreateStratisPowNode(this.powNetwork, "conn-9-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.powNetwork, "conn-9-node2").Start();

                Assert.True(node1.FullNode.ConnectionManager.Servers.Any());

                TestHelper.Connect(node1, node2);

                Assert.True(TestHelper.IsNodeConnectedTo(node2, node1));

                TestHelper.DisconnectAll(node1, node2);
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
