using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
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
            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize(Network.Main);
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var blockRepository = new BlockRepository(Network.Main, TestBase.CreateDataFolder(this), DateTimeProvider.Default, this.loggerFactory))
            {
                blockRepository.SetTxIndexAsync(true).Wait();

                var blocks = new List<Block>();
                for (int i = 0; i < 5; i++)
                {
                    var block = new Block();
                    block.AddTransaction(new Transaction());
                    block.AddTransaction(new Transaction());
                    block.Transactions[0].AddInput(new TxIn(Script.Empty));
                    block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                    block.Transactions[1].AddInput(new TxIn(Script.Empty));
                    block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                    block.UpdateMerkleRoot();
                    block.Header.HashPrevBlock = blocks.Any() ? blocks.Last().GetHash() : Network.Main.GenesisHash;
                    blocks.Add(block);
                }

                // put
                blockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // check the presence of each block in the repository
                foreach (var block in blocks)
                {
                    var received = blockRepository.GetAsync(block.GetHash()).GetAwaiter().GetResult();
                    Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                    foreach (var transaction in block.Transactions)
                    {
                        var trx = blockRepository.GetTrxAsync(transaction.GetHash()).GetAwaiter().GetResult();
                        Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                    }
                }

                // delete
                blockRepository.DeleteAsync(blocks.ElementAt(2).GetHash(), new[] { blocks.ElementAt(2).GetHash() }.ToList()).GetAwaiter().GetResult();
                var deleted = blockRepository.GetAsync(blocks.ElementAt(2).GetHash()).GetAwaiter().GetResult();
                Assert.Null(deleted);
            }
        }

        [Fact]
        public void BlockRepositoryBlockHash()
        {
            using (var blockRepo = new BlockRepository(Network.Main, TestBase.CreateDataFolder(this), DateTimeProvider.Default, this.loggerFactory))
            {
                blockRepo.InitializeAsync().GetAwaiter().GetResult();

                Assert.Equal(Network.Main.GenesisHash, blockRepo.BlockHash);
                var hash = new Block().GetHash();
                blockRepo.SetBlockHashAsync(hash).GetAwaiter().GetResult();
                Assert.Equal(hash, blockRepo.BlockHash);
            }
        }

        [Fact]
        public void BlockBroadcastInv()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                var stratisNode1 = builder.CreateStratisPowNode();
                var stratisNode2 = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratisWithMiner(10); // coinbase maturity = 10
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());

                // set node2 to use inv (not headers)
                stratisNode2.FullNode.ConnectionManager.ConnectedPeers.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // generate two new blocks
                stratisNodeSync.GenerateStratisWithMiner(2);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

                // wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            }
        }

        [Fact]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));

                stratisNodeSync.GenerateStratisWithMiner(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));

                // set the tip of best chain some blocks in the apst
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                stratisNodeSync.FullNode.Dispose();

                var newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.GetBlockStoreTip().HashBlock);
                //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));
            }
        }

        [Fact]
        public void BlockStoreCanReorg()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                var stratisNode1 = builder.CreateStratisPowNode();
                var stratisNode2 = builder.CreateStratisPowNode();
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
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 10);

                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // remove node 2
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode2.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisNode2));

                // mine some more with node 1
                stratisNode1.GenerateStratisWithMiner(10);

                // wait for node 1 to sync
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 20);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // remove node 1
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode1.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisNode1));

                // mine a higher chain with node2
                stratisNode2.GenerateStratisWithMiner(20);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.GetBlockStoreTip().Height == 30);

                // add node2
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                // node2 should be synced
                TestHelper.WaitLoop(() => stratisNode2.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);
            }
        }

        [Fact]
        public void BlockStoreIndexTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var stratisNode1 = builder.CreateStratisPowNode();
                var stratisNode2 = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNode1.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode1.FullNode.Network));
                stratisNode2.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode2.FullNode.Network));
                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);
                stratisNode1.GenerateStratisWithMiner(10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNode2.FullNode.GetBlockStoreTip().HashBlock);

                var bestBlock1 = stratisNode1.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx
                var trx = stratisNode2.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(bestBlock1.Transactions.First().GetHash()).Result;
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
