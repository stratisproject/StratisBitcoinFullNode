using System.Linq;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class SmartContractNodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var node1 = builder.CreateSmartContractPowNode();
                var node2 = builder.CreateSmartContractPowNode();
                builder.StartAll();
                node1.NotInIBD();
                node2.NotInIBD();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                var rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
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