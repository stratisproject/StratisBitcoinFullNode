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
        public async Task NodesCanConnectToEachOthersAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node1 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var node2 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
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
        public async Task CanStratisSyncFromCoreAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNode = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var coreNode = await builder.CreateNodeAsync().ConfigureAwait(false);
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                var tip = (await coreNode.FindBlockAsync(10).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreNode.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                //Now check if Core connect to stratis
                stratisNode.CreateRPCClient().RemoveNode(coreNode.Endpoint);
                tip = (await coreNode.FindBlockAsync(10).ConfigureAwait(false)).Last();
                coreNode.CreateRPCClient().AddNode(stratisNode.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public async Task CanStratisSyncFromStratisAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNode = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var stratisNodeSync = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var coreCreateNode = await builder.CreateNodeAsync().ConfigureAwait(false);
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
                stratisNodeSync.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                var tip = (await coreCreateNode.FindBlockAsync(5).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                bestBlockHash = stratisNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

            }
        }

        [Fact]
        public async Task CanCoreSyncFromStratisAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNode = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var coreNodeSync = await builder.CreateNodeAsync().ConfigureAwait(false);
                var coreCreateNode = await builder.CreateNodeAsync().ConfigureAwait(false);
                builder.StartAll();

                // not in IBD
                stratisNode.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                var tip = (await coreCreateNode.FindBlockAsync(5).ConfigureAwait(false)).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode.FullNode.HighestPersistedBlock().HashBlock == stratisNode.FullNode.Chain.Tip.HashBlock).ConfigureAwait(false);

                var bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                coreNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                await TestHelper.WaitLoopAsync(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public async Task Given__NodesAreSynced__When__ABigReorgHappens__Then__TheReorgIsIgnoredAsync()
        {
            // Temporary fix so the Network static initialize will not break.
            var m = Network.Main;
            try
            {
                using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
                {
                    var stratisMiner = await builder.CreateStratisPosNodeAsync().ConfigureAwait(false);
                    var stratisSyncer = await builder.CreateStratisPosNodeAsync().ConfigureAwait(false);
                    var stratisReorg = await builder.CreateStratisPosNodeAsync().ConfigureAwait(false);

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
                    await TestHelper.WaitLoopAsync(() => TestHelper.IsNodeSynced(stratisMiner)).ConfigureAwait(false);
                    stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                    stratisMiner.CreateRPCClient().AddNode(stratisSyncer.Endpoint, true);

                    await TestHelper.WaitLoopAsync(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer)).ConfigureAwait(false);
                    await TestHelper.WaitLoopAsync(() => TestHelper.AreNodesSynced(stratisMiner, stratisReorg)).ConfigureAwait(false);


                    // create a reorg by mining on two different chains
                    // ================================================

                    stratisMiner.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);

                    var t1 = Task.Run(() => stratisMiner.GenerateStratisWithMiner(10));
                    var t2 = Task.Run(() => stratisReorg.GenerateStratisWithMiner(12));
                    await Task.WhenAll(t1, t2).ConfigureAwait(false);
                    await TestHelper.WaitLoopAsync(() => TestHelper.IsNodeSynced(stratisMiner)).ConfigureAwait(false);
                    await TestHelper.WaitLoopAsync(() => TestHelper.IsNodeSynced(stratisReorg)).ConfigureAwait(false);

                    // The hash before the reorg node is connected.
                    var hashBeforeReorg = stratisMiner.FullNode.Chain.Tip.HashBlock;

                    // connect the reorg chain
                    stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                    stratisSyncer.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);

                    // wait for the chains to catch up
                    await TestHelper.WaitLoopAsync(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer)).ConfigureAwait(false);

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
