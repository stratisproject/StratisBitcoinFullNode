using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.IntegrationTests.MockChain;

namespace Stratis.SmartContracts.IntegrationTests.PoW.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    public class PoWMockChain : IMockChain
    {
        // TODO: This and PoAMockChain could share most logic

        private readonly SmartContractNodeBuilder builder;

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

        public PoWMockChain(int numNodes)
        {
            this.Network = new SmartContractsRegTest(); // TODO: Make this configurable.

            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];

            for (int i = 0; i < numNodes; i++)
            {
                CoreNode node = this.builder.CreateSmartContractPowNode().Start();

                // Add other nodes
                for (int j = 0; j < i; j++)
                {
                    MockChainNode otherNode = this.nodes[j];
                    TestHelper.Connect(node, otherNode.CoreNode);
                    TestHelper.Connect(otherNode.CoreNode, node);
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