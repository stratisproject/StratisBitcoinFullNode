using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public class BlockStoreTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly DBreezeSerializer dBreezeSerializer;

        public BlockStoreTests()
        {
            this.loggerFactory = new LoggerFactory();

            this.network = new BitcoinRegTest();
            this.dBreezeSerializer = new DBreezeSerializer(this.network);
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var blockRepository = new BlockRepository(this.network, TestBase.CreateDataFolder(this), this.loggerFactory, this.dBreezeSerializer))
            {
                blockRepository.SetTxIndexAsync(true).Wait();

                var blocks = new List<Block>();
                for (int i = 0; i < 5; i++)
                {
                    Block block = this.network.CreateBlock();
                    block.AddTransaction(this.network.CreateTransaction());
                    block.AddTransaction(this.network.CreateTransaction());
                    block.Transactions[0].AddInput(new TxIn(Script.Empty));
                    block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                    block.Transactions[1].AddInput(new TxIn(Script.Empty));
                    block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                    block.UpdateMerkleRoot();
                    block.Header.HashPrevBlock = blocks.Any() ? blocks.Last().GetHash() : this.network.GenesisHash;
                    blocks.Add(block);
                }

                // put
                blockRepository.PutAsync(new HashHeightPair(blocks.Last().GetHash(), blocks.Count), blocks).GetAwaiter().GetResult();

                // check the presence of each block in the repository
                foreach (Block block in blocks)
                {
                    Block received = blockRepository.GetBlockAsync(block.GetHash()).GetAwaiter().GetResult();
                    Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                    foreach (Transaction transaction in block.Transactions)
                    {
                        Transaction trx = blockRepository.GetTransactionByIdAsync(transaction.GetHash()).GetAwaiter().GetResult();
                        Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                    }
                }

                // delete
                blockRepository.DeleteAsync(new HashHeightPair(blocks.ElementAt(2).GetHash(), 2), new[] { blocks.ElementAt(2).GetHash() }.ToList()).GetAwaiter().GetResult();
                Block deleted = blockRepository.GetBlockAsync(blocks.ElementAt(2).GetHash()).GetAwaiter().GetResult();
                Assert.Null(deleted);
            }
        }

        [Fact]
        public void BlockBroadcastInv()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10NoWallet).Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10NoWallet).Start();

                // Sync both nodes
                TestHelper.ConnectAndSync(stratisNode1, stratisNodeSync);
                TestHelper.ConnectAndSync(stratisNode2, stratisNodeSync);

                // Set node2 to use inv (not headers).
                stratisNode2.FullNode.ConnectionManager.ConnectedPeers.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

                // Generate two new blocks.
                TestHelper.MineBlocks(stratisNodeSync, 2);

                // Wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));
            }
        }

        [Fact(Skip = "Investigate PeerConnector shutdown timeout issue")]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();

                // Set the tip of the best chain to some blocks in the past.
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // Stop the node to persist the chain with the reset tip.
                stratisNodeSync.FullNode.Dispose();

                CoreNode newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // Start the node, this should hit the block store recover code.
                newNodeInstance.Start();

                // Check that the store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.GetBlockStoreTip().HashBlock);
            }
        }

        [Fact]
        public void BlockStoreCanReorg()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();

                // Sync both nodes.
                TestHelper.ConnectAndSync(stratisNodeSync, stratisNode1);
                TestHelper.ConnectAndSync(stratisNodeSync, stratisNode2);

                // Remove node 2.
                TestHelper.Disconnect(stratisNodeSync, stratisNode2);

                // Mine some more with node 1
                TestHelper.MineBlocks(stratisNode1, 10);

                // Wait for node 1 to sync
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 20);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // Remove node 1.
                TestHelper.Disconnect(stratisNodeSync, stratisNode1);

                // Mine a higher chain with node 2.
                TestHelper.MineBlocks(stratisNode2, 20);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.GetBlockStoreTip().Height == 30);

                // Add node 2.
                TestHelper.Connect(stratisNodeSync, stratisNode2);

                // Node2 should be synced.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));
            }
        }

        [Fact]
        public void BlockStoreIndexTx()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10NoWallet).Start();

                // Sync both nodes.
                TestHelper.ConnectAndSync(stratisNode1, stratisNode2);

                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNode2.FullNode.GetBlockStoreTip().HashBlock);

                Block bestBlock1 = stratisNode1.FullNode.BlockStore().GetBlockAsync(stratisNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // Get the block coinbase trx.
                Transaction trx = stratisNode2.FullNode.BlockStore().GetTransactionByIdAsync(bestBlock1.Transactions.First().GetHash()).Result;
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }

        [Fact]
        public void GetBlockCanRetreiveGenesis()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPowNode(this.network).Start();
                uint256 genesisHash = node.FullNode.Chain.Genesis.HashBlock;
                Block genesisBlock = node.FullNode.BlockStore().GetBlockAsync(genesisHash).Result;
                Assert.Equal(genesisHash, genesisBlock.GetHash());
            }
        }
    }
}
