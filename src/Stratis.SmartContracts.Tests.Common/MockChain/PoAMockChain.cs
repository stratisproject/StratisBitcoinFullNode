using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    /// <remarks>TODO: This and PoWMockChain could share most logic</remarks>
    public class PoAMockChain : IMockChain
    {
        private readonly SmartContractNodeBuilder builder;
        private readonly Mnemonic initMnemonic;
        protected readonly MockChainNode[] nodes;
        public IReadOnlyList<MockChainNode> Nodes
        {
            get { return this.nodes; }
        }

        protected int chainHeight;

        public PoAMockChain(int numNodes, Mnemonic mnemonic = null)
        {
            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];
            this.initMnemonic = mnemonic;
        }

        public PoAMockChain Build()
        {
            var network = new SmartContractsPoARegTest();

            for (int nodeIndex = 0; nodeIndex < this.nodes.Length; nodeIndex++)
            {
                CoreNode node = this.builder.CreateSmartContractPoANode(network, nodeIndex).Start();

                for (int j = 0; j < nodeIndex; j++)
                {
                    MockChainNode otherNode = this.nodes[j];
                    TestHelper.Connect(node, otherNode.CoreNode);
                }

                this.nodes[nodeIndex] = new MockChainNode(node, this, this.initMnemonic);
            }

            return this;
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

        public void MineBlocks(int num)
        {
            this.nodes[0].CoreNode.MineBlocksAsync(num).GetAwaiter().GetResult();

            this.WaitForAllNodesToSync();
        }
    }
}