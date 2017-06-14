using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSync
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node1 = builder.CreateStratisNode();
                var node2 = builder.CreateStratisNode();
                builder.StartAll();
                Assert.Equal(0, node1.FullNode.ConnectionManager.ConnectedNodes.Count());
                Assert.Equal(0, node2.FullNode.ConnectionManager.ConnectedNodes.Count());
                var rpc1 = node1.CreateRPCClient();
                var rpc2 = node2.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Equal(1, node1.FullNode.ConnectionManager.ConnectedNodes.Count());
                Assert.Equal(1, node2.FullNode.ConnectionManager.ConnectedNodes.Count());

                var behavior = node1.FullNode.ConnectionManager.ConnectedNodes.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.False(behavior.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedNodes.First().Behaviors.Find<ConnectionManagerBehavior>();
                Assert.True(behavior.Inbound);
                Assert.False(behavior.OneTry);
            }
        }

        [Fact]
        public void CanStratisSyncFromCore()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNode = builder.CreateStratisNode();
                var coreNode = builder.CreateNode();
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                var tip = coreNode.FindBlock(10).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                //Now check if Core connect to stratis
                stratisNode.CreateRPCClient().RemoveNode(coreNode.Endpoint);
                tip = coreNode.FindBlock(10).Last();
                coreNode.CreateRPCClient().AddNode(stratisNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanStratisSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNode = builder.CreateStratisNode();
                var stratisNodeSync = builder.CreateStratisNode();
                var coreCreateNode = builder.CreateNode();
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
                stratisNodeSync.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = stratisNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

            }
        }

        [Fact]
        public void CanCoreSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNode = builder.CreateStratisNode();
                var coreNodeSync = builder.CreateNode();
                var coreCreateNode = builder.CreateNode();
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                var tip = coreCreateNode.FindBlock(5).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNode.FullNode.Chain.Tip.HashBlock);

                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                coreNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

    }
}
