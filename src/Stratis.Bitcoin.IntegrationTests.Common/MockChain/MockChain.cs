using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common.MockChain
{
    /// <summary>
    /// Facade for NodeBuilder.
    /// </summary>
    public class MockChain : IDisposable
    {
        private readonly NodeBuilder builder;

        private readonly MockChainNode[] nodes;

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

        public MockChain(int numNodes)
        {
            this.Network = new SmartContractsRegTest(); // TODO: Make this configurable.

            this.builder = NodeBuilder.Create(this);
            this.nodes = new MockChainNode[numNodes];

            for (int i = 0; i < numNodes; i++)
            {
                CoreNode node = this.builder.CreateSmartContractPowNode();
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
        internal void WaitForAllNodesToSync()
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