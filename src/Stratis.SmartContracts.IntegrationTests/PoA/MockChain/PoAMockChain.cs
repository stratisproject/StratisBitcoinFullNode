using System.Collections.Generic;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    public class PoAMockChain : IMockChain
    {
        // TODO: This and PoWMockChain could share most logic

        private readonly SmartContractNodeBuilder builder;

        protected readonly MockChainNode[] nodes;

        /// <summary>
        /// Nodes on this network.
        /// </summary>
        public IReadOnlyList<MockChainNode> Nodes
        {
            get { return this.nodes; }
        }

        public PoAMockChain(int numNodes)
        {
            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];
            var network = new SmartContractsPoARegTest();

            for (int nodeIndex = 0; nodeIndex < numNodes; nodeIndex++)
            {
                CoreNode node = this.builder.CreateSmartContractPoANode(network, nodeIndex).Start();

                for (int j = 0; j < nodeIndex; j++)
                {
                    MockChainNode otherNode = this.nodes[j];
                    TestHelper.Connect(node, otherNode.CoreNode);
                    TestHelper.Connect(otherNode.CoreNode, node);
                }

                this.nodes[nodeIndex] = new MockChainNode(node, this);
            }
        }

        /// <summary>
        /// Halts the main thread until all nodes on the network are synced.
        /// </summary>
        public void WaitForAllNodesToSync()
        {
            if (this.nodes.Length == 1)
            {
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[0].CoreNode));
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