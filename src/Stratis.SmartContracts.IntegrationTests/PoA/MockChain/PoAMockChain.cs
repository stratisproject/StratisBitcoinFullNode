using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.IntegrationTests.MockChain;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    public class PoAMockChain : IMockChain
    {
        // TODO: This and PoWMockChain could share most logic

        private readonly NodeBuilder builder;

        protected readonly MockChainNode[] nodes;

        /// <summary>
        /// Nodes on this network.
        /// </summary>
        public IReadOnlyList<MockChainNode> Nodes
        {
            get { return this.nodes; }
        }

        /// <summary>
        /// Network the nodes are running on.
        /// </summary>
        public Network Network { get; }

        public PoAMockChain(int numNodes)
        {
            this.builder = NodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];

            for (int i = 0; i < numNodes; i++)
            {
                CoreNode node = this.builder.CreateSmartContractPoANode();
                node.Start();
                // Add other nodes
                RPCClient rpcClient = node.CreateRPCClient();
                for (int j = 0; j < i; j++)
                {
                    MockChainNode otherNode = this.nodes[j];
                    rpcClient.AddNode(otherNode.CoreNode.Endpoint, true);
                    otherNode.CoreNode.CreateRPCClient().AddNode(node.Endpoint);
                }
                this.nodes[i] = new MockChainNode(node, this);
            }
        }

        /// <summary>
        /// Halts the main thread until all nodes on the network are synced.
        /// </summary>
        public void WaitForAllNodesToSync()
        {
            if (this.nodes.Length == 1)
            {
                TestHelper.WaitLoop(() => this.nodes[0].IsSynced);
                return;
            }

            for (int i = 0; i < this.nodes.Length - 1; i++)
            {
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodes[i].CoreNode, this.nodes[i + 1].CoreNode));
            }
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}