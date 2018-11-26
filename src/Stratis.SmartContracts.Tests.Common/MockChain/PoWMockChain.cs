using System.Collections.Generic;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    /// <remarks>TODO: This and PoAMockChain could share most logic.</remarks>
    public class PoWMockChain : IMockChain
    {
        private int height;

        private readonly SmartContractNodeBuilder builder;

        protected readonly MockChainNode[] nodes;
        public IReadOnlyList<MockChainNode> Nodes
        {
            get { return this.nodes; }
        }

        public PoWMockChain(int numberOfNodes)
        {
            this.height = 0;
            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodes = new MockChainNode[numberOfNodes];

            for (int nodeIndex = 0; nodeIndex < numberOfNodes; nodeIndex++)
            {
                CoreNode node = this.builder.CreateSmartContractPowNode().Start();

                for (int j = 0; j < nodeIndex; j++)
                {
                    MockChainNode otherNode = this.nodes[j];
                    TestHelper.Connect(node, otherNode.CoreNode);
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

        public void MineBlocks(int num)
        {
            for (int i = 0; i < num; i++)
            {
                int nodeToMineWith = this.height % this.Nodes.Count;

                this.Nodes[nodeToMineWith].MineBlocks(1);

                this.height++;
            }
        }

        public void WaitAllMempoolCount(int num)
        {
            for (int i = 0; i < this.Nodes.Count; i++)
            {
                this.Nodes[i].WaitMempoolCount(num);
            }
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}