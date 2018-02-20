using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractNodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node1 = builder.CreateSmartContractNode();
                var node2 = builder.CreateSmartContractNode();
                builder.StartAll();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                var rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.False(behavior.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.True(behavior.Inbound);
                Assert.False(behavior.OneTry);
            }
        }
    }
}
