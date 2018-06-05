using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public sealed class SmartContractNodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node1 = builder.CreateSmartContractNode();
                var node2 = builder.CreateSmartContractNode();
                builder.StartAll();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                var rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.False(behavior.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.True(behavior.Inbound);
                Assert.False(behavior.OneTry);
            }
        }

        [Fact]
        public void NodesSyncing()
        {
            SmartContractNetworkSimulator simulator = new SmartContractNetworkSimulator();

            simulator.Initialize(4);

            var miner = simulator.Nodes[0];
            var connector = simulator.Nodes[1];
            var networkNode1 = simulator.Nodes[2];
            var networkNode2 = simulator.Nodes[3];

            var minerNetwork = miner.FullNode.NodeService<Network>();
            var connectorNetwork = connector.FullNode.NodeService<Network>();
            var nodeOneNetwork = networkNode1.FullNode.NodeService<Network>();
            var nodeTwoNetwork = networkNode2.FullNode.NodeService<Network>();

            miner.CreateRPCClient().AddNode(connector.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode1.Endpoint, true);
            connector.CreateRPCClient().AddNode(networkNode2.Endpoint, true);
            networkNode1.CreateRPCClient().AddNode(networkNode2.Endpoint, true);

            simulator.MakeSureEachNodeCanMineAndSync();

            int networkHeight = miner.FullNode.Chain.Height;
            Assert.Equal(networkHeight, simulator.Nodes.Count);

            // Random node on network generates a block.
            networkNode1.GenerateSmartContractStratis(1);

            // Wait until connector get the hash of network's block.
            while ((connector.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != networkNode1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) ||
                   (networkNode1.FullNode.ChainBehaviorState.ConsensusTip.Height == networkHeight))
                Thread.Sleep(1);

            // Make sure that miner did not advance yet but connector did.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight + 1);

            // Miner mines the block.
            miner.GenerateSmartContractStratis(1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

            networkHeight++;

            // Make sure that at this moment miner's tip != network's and connector's tip.
            Assert.NotEqual(miner.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(connector.FullNode.Chain.Tip.HashBlock, networkNode1.FullNode.Chain.Tip.HashBlock);
            Assert.Equal(miner.FullNode.Chain.Tip.Height, networkHeight);
            Assert.Equal(connector.FullNode.Chain.Tip.Height, networkHeight);

            connector.GenerateSmartContractStratis(1);
            networkHeight++;

            int delay = 0;

            while (true)
            {
                Thread.Sleep(50);
                if (simulator.DidAllNodesReachHeight(networkHeight))
                    break;
                delay += 50;

                Assert.True(delay < 10 * 1000, "Miner node was not able to advance!");
            }

            Assert.Equal(networkNode1.FullNode.Chain.Tip.HashBlock, miner.FullNode.Chain.Tip.HashBlock);
        }
    }
}