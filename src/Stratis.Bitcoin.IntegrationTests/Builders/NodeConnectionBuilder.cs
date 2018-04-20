﻿using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Builders
{
    public class NodeConnectionBuilder
    {
        private readonly NodeGroupBuilder parentNodeGroupBuilder;
        private IDictionary<string, CoreNode> nodes;
        private SharedSteps sharedSteps;

        private NodeConnectionBuilder()
        {
            this.sharedSteps = new SharedSteps();
        }

        public NodeConnectionBuilder(NodeGroupBuilder parentNodeGroupBuilder) : this()
        {
            this.parentNodeGroupBuilder = parentNodeGroupBuilder;
        }

        public NodeConnectionBuilder(IDictionary<string, CoreNode> nodesDictionary) : this()
        {
            this.nodes = nodesDictionary;
        }

        public NodeConnectionBuilder With(IDictionary<string, CoreNode> nodes)
        {
            this.nodes = nodes;
            return this;
        }

        public NodeConnectionBuilder Connect(string from, string to)
        {
            this.nodes[from].CreateRPCClient().AddNode(this.nodes[to].Endpoint, true);
            this.sharedSteps.WaitForNodesToSync(this.nodes[from], this.nodes[to]);
            return this;
        }

        public NodeGroupBuilder AndNoMoreConnections()
        {
            if (this.parentNodeGroupBuilder == null)
                throw new NotSupportedException("Pass parent builder into constructor if you need to return to that builder to continue building.");

            return this.parentNodeGroupBuilder;
        }

        public void DisconnectAll()
        {
            foreach (var node in this.nodes)
            {
                foreach (var otherNode in this.nodes.Where(x => x.Key != node.Key))
                {
                    node.Value.CreateRPCClient().RemoveNode(otherNode.Value.Endpoint);
                }
            }
        }
    }
}