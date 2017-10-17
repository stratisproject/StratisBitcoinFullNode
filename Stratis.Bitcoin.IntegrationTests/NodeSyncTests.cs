using NBitcoin;
using Stratis.Bitcoin.Connection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node1 = builder.CreateStratisPowNode();
                var node2 = builder.CreateStratisPowNode();
                builder.StartAll();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedNodes);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedNodes);
                var rpc1 = node1.CreateRPCClient();
                var rpc2 = node2.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedNodes);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedNodes);

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
                var stratisNode = builder.CreateStratisPowNode();
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
                var stratisNode = builder.CreateStratisPowNode();
                var stratisNodeSync = builder.CreateStratisPowNode();
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
                var stratisNode = builder.CreateStratisPowNode();
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
                TestHelper.WaitLoop(() => stratisNode.FullNode.HighestPersistedBlock().HashBlock == stratisNode.FullNode.Chain.Tip.HashBlock);

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

        [Fact]
        public void Given__NodesAreSynced__When__ABigReorgHappens__Then__TheReorgIsIgnored()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            try
            {
                using (NodeBuilder builder = NodeBuilder.Create())
                {
                    var stratisMiner = builder.CreateStratisPosNode();
                    var stratisSyncer = builder.CreateStratisPosNode();
                    var stratisReorg = builder.CreateStratisPosNode();

                    builder.StartAll();
                    stratisMiner.NotInIBD();
                    stratisSyncer.NotInIBD();
                    stratisReorg.NotInIBD();

                    // TODO: set the max allowed reorg threshold here
                    // assume a reorg of 10 blocks is not allowed.

                    stratisMiner.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisMiner.FullNode.Network));
                    stratisReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisReorg.FullNode.Network));

                    stratisMiner.GenerateStratisWithMiner(1);

                    // wait for block repo for block sync to work
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisMiner));
                    stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                    stratisMiner.CreateRPCClient().AddNode(stratisSyncer.Endpoint, true);

                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisReorg));


                    // create a reorg by mining on two different chains
                    // ================================================

                    stratisMiner.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);

                    var t1 = Task.Run(() => stratisMiner.GenerateStratisWithMiner(10));
                    var t2 = Task.Run(() => stratisReorg.GenerateStratisWithMiner(12));
                    Task.WaitAll(t1, t2);
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisMiner));
                    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisReorg));

                    // The hash before the reorg node is connected.
                    var hashBeforeReorg = stratisMiner.FullNode.Chain.Tip.HashBlock;

                    // connect the reorg chain
                    stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                    stratisSyncer.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);

                    // wait for the chains to catch up
                    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));

                    // check that a reorg did not happen.
                    Assert.Equal(hashBeforeReorg, stratisSyncer.FullNode.Chain.Tip.HashBlock);
                }
            }
            finally
            {
                Transaction.TimeStamp = false;
                Block.BlockSignature = false;
            }
        }
    }
}
