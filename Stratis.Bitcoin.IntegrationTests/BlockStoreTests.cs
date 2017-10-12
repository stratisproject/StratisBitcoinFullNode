using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Xunit;
using BlockRepository = Stratis.Bitcoin.Features.BlockStore.BlockRepository;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class BlockStoreTests
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public BlockStoreTests()
        {
            this.loggerFactory = new LoggerFactory();
        }

        private async Task BlockRepositoryBenchAsync()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, this.loggerFactory))
                {
                    var lst = new List<Block>();
                    for (int i = 0; i < 30; i++)
                    {
                        // roughly 1mb blocks
                        var block = new Block();
                        for (int j = 0; j < 3000; j++)
                        {
                            var trx = new Transaction();
                            block.AddTransaction(new Transaction());
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            trx.AddInput(new TxIn(Script.Empty));
                            trx.AddOutput(Money.COIN + j + i + 1, new Script(Guid.NewGuid().ToByteArray()
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())
                                .Concat(Guid.NewGuid().ToByteArray())));
                            block.AddTransaction(trx);
                        }
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    await blockRepo.PutAsync(lst.Last().GetHash(), lst).ConfigureAwait(false);
                    var first = stopwatch.ElapsedMilliseconds;
                    await blockRepo.PutAsync(lst.Last().GetHash(), lst).ConfigureAwait(false);
                    var second = stopwatch.ElapsedMilliseconds;

                }
            }
        }

        [Fact]
        public async Task BlockRepositoryPutBatchAsync()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, this.loggerFactory))
                {
                    await blockRepo.SetTxIndex(true).ConfigureAwait(false);

                    var lst = new List<Block>();
                    for (int i = 0; i < 5; i++)
                    {
                        // put
                        var block = new Block();
                        block.AddTransaction(new Transaction());
                        block.AddTransaction(new Transaction());
                        block.Transactions[0].AddInput(new TxIn(Script.Empty));
                        block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                        block.Transactions[1].AddInput(new TxIn(Script.Empty));
                        block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                        block.UpdateMerkleRoot();
                        block.Header.HashPrevBlock = lst.Any() ? lst.Last().GetHash() : Network.Main.GenesisHash;
                        lst.Add(block);
                    }

                    await blockRepo.PutAsync(lst.Last().GetHash(), lst).ConfigureAwait(false);

                    // check each block
                    foreach (var block in lst)
                    {
                        var received = await blockRepo.GetAsync(block.GetHash()).ConfigureAwait(false);
                        Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                        foreach (var transaction in block.Transactions)
                        {
                            var trx = await blockRepo.GetTrxAsync(transaction.GetHash()).ConfigureAwait(false);
                            Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                        }
                    }

                    // delete
                    await blockRepo.DeleteAsync(lst.ElementAt(2).GetHash(), new[] {lst.ElementAt(2).GetHash()}.ToList()).ConfigureAwait(false);
                    var deleted = await blockRepo.GetAsync(lst.ElementAt(2).GetHash()).ConfigureAwait(false);
                    Assert.Null(deleted);
                }
            }
        }

        [Fact]
        public async Task BlockRepositoryBlockHashAsync()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName, this.loggerFactory))
                {
                    await blockRepo.Initialize().ConfigureAwait(false);

                    Assert.Equal(Network.Main.GenesisHash, blockRepo.BlockHash);
                    var hash = new Block().GetHash();
                    await blockRepo.SetBlockHash(hash).ConfigureAwait(false);
                    Assert.Equal(hash, blockRepo.BlockHash);
                }
            }
        }

        [Fact]
        public async Task BlockBroadcastInvAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNodeSync = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var stratisNode1 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var stratisNode2 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                builder.StartAll();
                stratisNodeSync.NotInIBD();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratisWithMiner(10); // coinbase maturity = 10
                // wait for block repo for block sync to work
                await TestHelper.WaitLoopAsync(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock).ConfigureAwait(false);

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                await TestHelper.WaitLoopAsync(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);

                // set node2 to use inv (not headers)
                stratisNode2.FullNode.ConnectionManager.ConnectedNodes.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // generate two new blocks
                stratisNodeSync.GenerateStratisWithMiner(2);
                // wait for block repo for block sync to work
                await TestHelper.WaitLoopAsync(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock).ConfigureAwait(false);
                Features.BlockStore.IBlockRepository blockRepository = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository;
                Block hash = await blockRepository.GetAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => hash != null).ConfigureAwait(false);

                // wait for the other nodes to pick up the newly generated blocks
                await TestHelper.WaitLoopAsync(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task BlockStoreCanRecoverOnStartupAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNodeSync = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));

                stratisNodeSync.GenerateStratisWithMiner(10);
                await TestHelper.WaitLoopAsync(() => TestHelper.IsNodeSynced(stratisNodeSync)).ConfigureAwait(false);

                // set the tip of best chain some blocks in the apst
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                stratisNodeSync.FullNode.Stop();

                var newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // load the node, this should hit the block store recover code
                await newNodeInstance.StartAsync().ConfigureAwait(false);

                // check that store recovered to be the same as the best chain.
               Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.HighestPersistedBlock().HashBlock);
                //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));
            }
        }

        [Fact]
        public async Task BlockStoreCanReorgAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNodeSync = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var stratisNode1 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                var stratisNode2 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false);
                builder.StartAll();
                stratisNodeSync.NotInIBD();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                // sync both nodes
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode1.Endpoint, true);
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                stratisNode1.GenerateStratisWithMiner(10);
                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().Height == 10).ConfigureAwait(false);

                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode2.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock).ConfigureAwait(false);

                // remove node 2
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode2.Endpoint);

                // mine some more with node 1
                stratisNode1.GenerateStratisWithMiner(10);

                // wait for node 1 to sync
                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().Height == 20).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock).ConfigureAwait(false);

                // remove node 1
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode1.Endpoint);

                // mine a higher chain with node2
                stratisNode2.GenerateStratisWithMiner(20);
                await TestHelper.WaitLoopAsync(() => stratisNode2.FullNode.HighestPersistedBlock().Height == 30).ConfigureAwait(false);

                // add node2 
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                // node2 should be synced
                await TestHelper.WaitLoopAsync(() => stratisNode2.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task BlockStoreIndexTxAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var stratisNode1 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false); ;
                var stratisNode2 = await builder.CreateStratisPowNodeAsync().ConfigureAwait(false); ;
                builder.StartAll();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode1.FullNode.Network));
                stratisNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode2.FullNode.Network));
                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);
                stratisNode1.GenerateStratisWithMiner(10);
                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().Height == 10).ConfigureAwait(false);
                await TestHelper.WaitLoopAsync(() => stratisNode1.FullNode.HighestPersistedBlock().HashBlock == stratisNode2.FullNode.HighestPersistedBlock().HashBlock).ConfigureAwait(false);

                var bestBlock1 = await stratisNode1.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNode1.FullNode.Chain.Tip.HashBlock).ConfigureAwait(false);
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx 
                var trx = await stratisNode2.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(bestBlock1.Transactions.First().GetHash()).ConfigureAwait(false);
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
