using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Builders
{
    public class NodeGroupBuilder : IDisposable
    {
        private readonly NodeBuilder nodeBuilder;
        private readonly Dictionary<string, CoreNode> nodes;

        public NodeGroupBuilder(string testFolder)
        {
            this.nodeBuilder = NodeBuilder.Create(caller: testFolder);
            this.nodes = new Dictionary<string, CoreNode>();
        }

        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        public IDictionary<string, CoreNode> Build()
        {
            return this.nodes;
        }

        public NodeGroupBuilder StratisPowNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowNode());
            return this;
        }

        public NodeGroupBuilder CreateStratisPowMiningNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowMiningNode());
            return this;
        }

        public NodeGroupBuilder CreateStratisPosNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPosNode());
            return this;
        }

        public NodeGroupBuilder CreateStratisPosApiNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPosApiNode());
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