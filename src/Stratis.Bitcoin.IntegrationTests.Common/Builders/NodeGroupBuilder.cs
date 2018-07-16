using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common.Builders
{
    public class NodeGroupBuilder : IDisposable
    {
        private readonly NodeBuilder nodeBuilder;
        private readonly Dictionary<string, CoreNode> nodes;
        public readonly Dictionary<string, Mnemonic> NodeMnemonics;

        public NodeGroupBuilder(string testFolder)
        {
            this.nodeBuilder = NodeBuilder.Create(testFolder);
            this.nodes = new Dictionary<string, CoreNode>();
            this.NodeMnemonics = new Dictionary<string, Mnemonic>();
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

        public NodeGroupBuilder StratisCustomPowNode(string nodeName, NodeConfigParameters configParameters)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisCustomPowNode(configParameters));
            return this;
        }

        public NodeGroupBuilder CreateStratisPowApiNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowApiNode());
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
            Mnemonic mnemonic = this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(walletPassword, walletName);
            this.NodeMnemonics.Add(this.nodes.Last().Key, mnemonic);
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