using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Builders
{
    public class NodeGroupBuilder : IDisposable
    {
        private readonly NodeBuilder nodeBuilder;

        private Dictionary<string, CoreNode> nodes;

        private bool sync;

        public NodeGroupBuilder()
        {
            this.nodeBuilder = NodeBuilder.Create();
            this.nodes = new Dictionary<string, CoreNode>();
        }

        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        public IDictionary<string, CoreNode> Build()
        {
            CoreNode previousNode = null;

            return this.nodes;
        }

        public NodeGroupBuilder StratisPowNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowNode());
            return this;
        }

        public NodeGroupBuilder NotInIBD()
        {
            this.nodes.Last().Value.NotInIBD();
            return this;
        }

        public NodeGroupBuilder WithWallet(string walletName, string walletPassword)
        {
            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(walletPassword, walletName);
            return this;
        }

        public NodeGroupBuilder Start()
        {
            this.nodes.Last().Value.Start();
            return this;
        }

        public NodeConnectionBuilder WithConnections()
        {
            return new NodeConnectionBuilder(this).With(this.nodes);
        }
    }
}