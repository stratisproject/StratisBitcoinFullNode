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
using Xunit;
using BlockRepository = Stratis.Bitcoin.Features.BlockStore.BlockRepository;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class BlockStoreTests
    {
        private void BlockRepositoryBench()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName))
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
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var first = stopwatch.ElapsedMilliseconds;
                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();
                    var second = stopwatch.ElapsedMilliseconds;

                }
            }
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName))
                {
                    blockRepo.SetTxIndex(true).Wait();

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

                    blockRepo.PutAsync(lst.Last().GetHash(), lst).GetAwaiter().GetResult();

                    // check each block
                    foreach (var block in lst)
                    {
                        var received = blockRepo.GetAsync(block.GetHash()).GetAwaiter().GetResult();
                        Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                        foreach (var transaction in block.Transactions)
                        {
                            var trx = blockRepo.GetTrxAsync(transaction.GetHash()).GetAwaiter().GetResult();
                            Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                        }
                    }

                    // delete
                    blockRepo.DeleteAsync(lst.ElementAt(2).GetHash(), new[] {lst.ElementAt(2).GetHash()}.ToList());
                    var deleted = blockRepo.GetAsync(lst.ElementAt(2).GetHash()).GetAwaiter().GetResult();
                    Assert.Null(deleted);
                }
            }
        }

        [Fact]
        public void BlockRepositoryBlockHash()
        {
            using (var dir = TestDirectory.Create())
            {
                using (var blockRepo = new BlockRepository(Network.Main, dir.FolderName))
                {
                    blockRepo.Initialize().GetAwaiter().GetResult();

                    Assert.Equal(Network.Main.GenesisHash, blockRepo.BlockHash);
                    var hash = new Block().GetHash();
                    blockRepo.SetBlockHash(hash).GetAwaiter().GetResult();
                    Assert.Equal(hash, blockRepo.BlockHash);
                }
            }
        }

        [Fact]
        public void BlockBroadcastInv()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                var stratisNode1 = builder.CreateStratisNode();
                var stratisNode2 = builder.CreateStratisNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratisWithMiner(10); // coinbase maturity = 10
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());

                // set node2 to use inv (not headers)
                stratisNode2.FullNode.ConnectionManager.ConnectedNodes.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // generate two new blocks
                stratisNodeSync.GenerateStratisWithMiner(2);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

                // wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            }
        }

        [Fact]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));

                stratisNodeSync.GenerateStratisWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));

                // set the tip of best chain some blocks in the apst
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                stratisNodeSync.FullNode.Stop();

                var newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
               Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);
                //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));
            }
        }

        [Fact]
        public void BlockStoreCanReorg()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                var stratisNode1 = builder.CreateStratisNode();
                var stratisNode2 = builder.CreateStratisNode();
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
                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.Height == 10);

                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);

                // remove node 2
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode2.Endpoint);

                // mine some more with node 1
                stratisNode1.GenerateStratisWithMiner(10);

                // wait for node 1 to sync
                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.Height == 20);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);

                // remove node 1
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode1.Endpoint);

                // mine a higher chain with node2
                stratisNode2.GenerateStratisWithMiner(20);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.ChainBehaviorState.HighestPersistedBlock.Height == 30);

                // add node2 
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                // node2 should be synced
                TestHelper.WaitLoop(() => stratisNode2.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);
            }
        }

        [Fact]
        public void BlockStoreIndexTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNode1 = builder.CreateStratisNode();
                var stratisNode2 = builder.CreateStratisNode();
                builder.StartAll();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode1.FullNode.Network));
                stratisNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode2.FullNode.Network));
                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);
                stratisNode1.GenerateStratisWithMiner(10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.Height == 10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNode2.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock);

                var bestBlock1 = stratisNode1.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx 
                var trx = stratisNode2.FullNode.BlockStoreManager.BlockRepository.GetTrxAsync(bestBlock1.Transactions.First().GetHash()).Result;
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
