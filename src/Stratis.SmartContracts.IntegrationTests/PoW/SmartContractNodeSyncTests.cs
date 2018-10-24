﻿using System.Linq;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public sealed class SmartContractNodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node1 = builder.CreateSmartContractPowNode().Start();
                var node2 = builder.CreateSmartContractPowNode().Start();

                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);

                TestHelper.Connect(node1, node2);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.OfType<IConnectionManagerBehavior>().FirstOrDefault();
                Assert.False(behavior.AttachedPeer.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.OfType<IConnectionManagerBehavior>().FirstOrDefault();
                Assert.True(behavior.AttachedPeer.Inbound);
                Assert.False(behavior.OneTry);
            }
        }
    }
}