﻿using System.Collections.Generic;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    /// <remarks>TODO: This and PoWMockChain could share most logic</remarks>
    public class PoAMockChain : IMockChain
    {
        private readonly SmartContractNodeBuilder builder;

        protected readonly MockChainNode[] nodes;
        public IReadOnlyList<MockChainNode> Nodes
        {
            get { return this.nodes; }
        }

        protected int chainHeight;

        public PoAMockChain(int numNodes)
        {
            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];
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

                this.nodes[nodeIndex] = new MockChainNode(node, this);
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

        public void Dispose()
        {
            this.builder.Dispose();
        }

        public void MineBlocks(int num)
        {
            int currentHeight = this.nodes[0].CoreNode.GetTip().Height;

            for (int i = 0; i < num; i++)
            {
                this.builder.PoATimeProvider.NextSpacing();
                TestHelper.WaitLoop(() => this.nodes[0].CoreNode.GetTip().Height == currentHeight + 1);
                currentHeight++;
            }
            WaitForAllNodesToSync();
        }
    }
}