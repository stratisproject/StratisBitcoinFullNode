﻿using System;
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
        private readonly Network network;

        public NodeGroupBuilder(string testFolder, Network network)
        {
            this.nodeBuilder = NodeBuilder.Create(testFolder);
            this.nodes = new Dictionary<string, CoreNode>();
            this.NodeMnemonics = new Dictionary<string, Mnemonic>();
            this.network = network;
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
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowNode(this.network));
            return this;
        }

        public NodeGroupBuilder StratisCustomPowNode(string nodeName, NodeConfigParameters configParameters)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisCustomPowNode(this.network, configParameters));
            return this;
        }

        public NodeGroupBuilder CreateStratisPowApiNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowApiNode(this.network));
            return this;
        }

        public NodeGroupBuilder CreateStratisPosNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPosNode(this.network));
            return this;
        }

        public NodeGroupBuilder CreateStratisPosApiNode(string nodeName)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPosApiNode(this.network));
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