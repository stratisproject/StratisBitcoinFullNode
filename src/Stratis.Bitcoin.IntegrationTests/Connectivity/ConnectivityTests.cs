using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Connectivity
{
    public class ConnectivityTests
    {
        private readonly Network network;

        public ConnectivityTests()
        {
            this.network = new StratisRegTest();
        }
        
        [Fact]
        public async Task EnsurePeerInAddressManagerAlsoConnectsAndSeenInPeerAddressManagerBehaviour()
        {
            // node1 connects to node2.
            // node3 adds node2 to the Address Manager.
            // node3 connects to node1 - and picks up node2 to connect to.
            // Extra check to see node2 in the AddressManagerBehaviour.

            const string PeerAddressManagerMemberName = "peerAddressManager";

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node3 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();

                var node1ConnectionMgr = node1.FullNode.NodeService<IConnectionManager>();

                // node1 connects to node2.
                TestHelper.Connect(node2, node1);
                TestHelper.WaitLoop(() => node1ConnectionMgr.ConnectedPeers.Count() == 1);

                // node3 adds node2 to the Address Manager.
                var node3ConnectionMgr = node3.FullNode.NodeService<IConnectionManager>();
                var node3PeerAddressManager = node3ConnectionMgr.GetMemberValue(PeerAddressManagerMemberName) as PeerAddressManager;

                var node3PeersDictionary = node3PeerAddressManager.GetMemberValue("peers") as ConcurrentDictionary<IPEndPoint, PeerAddress>;
                node3PeersDictionary.TryAdd(node2.Endpoint, new PeerAddress() { Endpoint = node2.Endpoint });

                // node3 connects to node1 - and picks up node2 to connect to.
                TestHelper.Connect(node3, node1);
                TestHelper.WaitLoop(() => node3ConnectionMgr.ConnectedPeers.Count() == 2);

                node3ConnectionMgr.ConnectedPeers.Should().Contain(c => c.PeerEndPoint.Port == node2.Endpoint.Port);

                // Extra check to see node2 in the AddressManagerBehaviour.
                var node3PeerAddressManagerBehaviour = node3ConnectionMgr.ConnectedPeers.First().
                    Behaviors.Where(b => b.GetType() == typeof(PeerAddressManagerBehaviour)).FirstOrDefault();

                var behaviourPeerAddressManager = node3PeerAddressManagerBehaviour?.GetMemberValue(PeerAddressManagerMemberName) as PeerAddressManager;

                behaviourPeerAddressManager.FindPeer(node2.Endpoint).Should().NotBeNull();
            }
        }

        [Fact]
        public void WhenConnectingWithAddnodeConnectToPeerAndAnyPeersInTheAddressManager()
        {
            // TS101_Connectivity_CallAddNode.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node3 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode syncerNode = builder.CreateStratisPosNode(this.network).NotInIBD().Start();

                // Connects with AddNode inside Connect().
                TestHelper.Connect(node1, syncerNode);

                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);

                var syncerConnectionMgr = syncerNode.FullNode.NodeService<IConnectionManager>();

                var syncerAddNodeConnector = syncerConnectionMgr.PeerConnectors.
                    Where(p => p.GetType() == typeof(PeerConnectorAddNode)).First() as PeerConnectorAddNode;

                // Adding these endpoints will add the items to the internal address manager.
                syncerAddNodeConnector.ConnectionSettings.AddNode = new List<IPEndPoint>() { node2.Endpoint, node3.Endpoint };

                syncerConnectionMgr.Initialize(syncerNode.FullNode.NodeService<IConsensusManager>());

                TestHelper.WaitLoop(() => syncerConnectionMgr.ConnectedPeers.Count() == 3);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node2.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node3.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
            }
        }

        [Fact]
        public void WhenConnectingWithConnectOnlyConnectToTheRequestedPeer()
        {
            // TS102_Connectivity_CallConnect.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();

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
        public void IfBannedNodeTriesToConnectItFailsToEstablishConnection()
        {

            // TS105_Connectivity_PreventConnectingToBannedNodes.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();

                node1 = BanNode(node1, node2);

                Action connectAction = () => TestHelper.Connect(node1, node2);
                connectAction.Should().Throw<RPCException>().WithMessage("Internal error");

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();

                node1 = RemoveBan(node1, node2);

                TestHelper.Connect(node1, node2);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task NotFailIfTryToConnectToNonExistingNodeAsync()
        {
            // TS106_Connectivity_CanErrorHandleConnectionToNonExistingNodes.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(this.network).NotInIBD().Start();

                var node1ConnectionMgr = node1.FullNode.NodeService<IConnectionManager>();

                var node1PeerNodeConnector = node1ConnectionMgr.PeerConnectors.
                    Where(p => p.GetType() == typeof(PeerConnectorConnectNode)).First() as PeerConnectorConnectNode;

                var nonExistentEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 90);
                node1PeerNodeConnector.ConnectionSettings.Connect = new List<IPEndPoint>() { nonExistentEndpoint };

                node1ConnectionMgr.Initialize(node1.FullNode.NodeService<IConsensusManager>());

                await node1PeerNodeConnector.OnConnectAsync().ConfigureAwait(false);

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
