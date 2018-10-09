using System.Collections.Generic;
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
        protected readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly Network regTest;

        public BlockStoreTests()
        {
            this.loggerFactory = new LoggerFactory();

            this.network = KnownNetworks.Main;
            this.regTest = KnownNetworks.RegTest;
            var serializer = new DBreezeSerializer();
            serializer.Initialize(this.network);
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            using (var blockRepository = new BlockRepository(this.network, TestBase.CreateDataFolder(this), DateTimeProvider.Default, this.loggerFactory))
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
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.regTest).NotInIBD().WithWallet();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.regTest).NotInIBD();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.regTest).NotInIBD();

                builder.StartAll();

                // generate blocks and wait for the downloader to pickup
                TestHelper.MineBlocks(stratisNodeSync, 10); // coinbase maturity = 10

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
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
                TestHelper.MineBlocks(stratisNodeSync, 2);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

                // wait for the other nodes to pick up the newly generated blocks
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            }
        }

        [Retry]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.regTest).NotInIBD().WithWallet();

                builder.StartAll();

                TestHelper.MineBlocks(stratisNodeSync, 10);

                // set the tip of best chain some blocks in the apst
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                stratisNodeSync.FullNode.Dispose();

                CoreNode newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

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
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.regTest).NotInIBD();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.regTest).NotInIBD().WithWallet();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.regTest).NotInIBD().WithWallet();

                builder.StartAll();

                // sync both nodes
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode1.Endpoint, true);
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                TestHelper.MineBlocks(stratisNode1, 10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 10);

                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);
                TestHelper.WaitLoop(() => stratisNode2.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // remove node 2
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode2.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisNode2));

                // mine some more with node 1
                TestHelper.MineBlocks(stratisNode1, 10);

                // wait for node 1 to sync
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 20);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // remove node 1
                stratisNodeSync.CreateRPCClient().RemoveNode(stratisNode1.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisNode1));

                // mine a higher chain with node2
                TestHelper.MineBlocks(stratisNode2, 20);
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
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.regTest).NotInIBD().WithWallet();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.regTest).NotInIBD();

                builder.StartAll();

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNode2.Endpoint, true);

                TestHelper.MineBlocks(stratisNode1, 10);

                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().Height == 10);
                TestHelper.WaitLoop(() => stratisNode1.FullNode.GetBlockStoreTip().HashBlock == stratisNode2.FullNode.GetBlockStoreTip().HashBlock);

                Block bestBlock1 = stratisNode1.FullNode.BlockStore().GetBlockAsync(stratisNode1.FullNode.Chain.Tip.HashBlock).Result;
                Assert.NotNull(bestBlock1);

                // get the block coinbase trx
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
                CoreNode node = builder.CreateStratisPowNode(this.regTest).NotInIBD();
                builder.StartAll();
                uint256 genesisHash = node.FullNode.Chain.Genesis.HashBlock;
                Block genesisBlock = node.FullNode.BlockStore().GetBlockAsync(genesisHash).Result;
                Assert.Equal(genesisHash, genesisBlock.GetHash());
            }
        }
    }
}
