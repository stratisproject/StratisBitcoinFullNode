using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreTests
    {
        private BlockStoreQueue blockStoreQueue;
        private readonly IChainState chainState;
        private readonly Mock<IInitialBlockDownloadState> initialBlockDownloadState;
        private readonly NodeLifetime nodeLifetime;
        private ChainIndexer chainIndexer;
        private readonly Network network;
        private HashHeightPair repositoryTipHashAndHeight;
        private readonly Mock<IBlockRepository> blockRepositoryMock;
        private int repositorySavesCount = 0;
        private int repositoryTotalBlocksSaved = 0;
        private int repositoryTotalBlocksDeleted = 0;
        private Random random;

        private Dictionary<uint256, Block> listOfSavedBlocks;

        private readonly string testBlockHex = "07000000af72d939050259913e440b23bee62e3b9604129ec8424d265a6ee4916e060000a5a2cbad28617657336403daf202b797bfc4b9c5cfc65a258f32ec33ec9ad485314ea957ffff0f1e812b07000101000000184ea957010000000000000000000000000000000000000000000000000000000000000000ffffffff03510101ffffffff010084d717000000001976a9140099e795d9ee809dc74dce32c79d26db0265072488ac0000000000";

        public BlockStoreTests()
        {
            this.network = KnownNetworks.StratisMain;
            this.repositoryTipHashAndHeight = new HashHeightPair(this.network.GenesisHash, 0);

            this.random = new Random();

            this.listOfSavedBlocks = new Dictionary<uint256, Block>();
            this.listOfSavedBlocks.Add(uint256.One, Block.Parse(this.testBlockHex, KnownNetworks.StratisMain.Consensus.ConsensusFactory));

            this.chainIndexer = CreateChain(10);

            this.nodeLifetime = new NodeLifetime();

            this.blockRepositoryMock = new Mock<IBlockRepository>();
            this.blockRepositoryMock.Setup(x => x.PutAsync(It.IsAny<HashHeightPair>(), It.IsAny<List<Block>>()))
                .Returns((HashHeightPair newTip, List<Block> blocks) =>
            {
                this.repositoryTipHashAndHeight = newTip;
                this.repositorySavesCount++;
                this.repositoryTotalBlocksSaved += blocks.Count;
                return Task.CompletedTask;
            });

            this.blockRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<HashHeightPair>(), It.IsAny<List<uint256>>()))
                .Returns((HashHeightPair newTip, List<uint256> blocks) =>
            {
                this.repositoryTotalBlocksDeleted += blocks.Count;
                return Task.CompletedTask;
            });

            this.blockRepositoryMock.Setup(x => x.GetBlockAsync(It.IsAny<uint256>()))
                .Returns((uint256 hash) =>
            {
                Block block = null;

                if (this.listOfSavedBlocks.ContainsKey(hash))
                    block = this.listOfSavedBlocks[hash];

                return Task.FromResult(block);
            });

            this.blockRepositoryMock.Setup(x => x.TipHashAndHeight).Returns(() =>
            {
                return this.repositoryTipHashAndHeight;
            });

            this.chainState = new ChainState();
            this.initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();

            var blockStoreFlushCondition = new BlockStoreQueueFlushCondition(this.chainState, this.initialBlockDownloadState.Object);

            this.blockStoreQueue = new BlockStoreQueue(this.chainIndexer, this.chainState, blockStoreFlushCondition, new StoreSettings(NodeSettings.Default(this.network)),
                this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);
        }

        private ChainIndexer CreateChain(int blocksCount)
        {
            var chain = new ChainIndexer(this.network);
            for (int i = 0; i < blocksCount; i++)
            {
                BlockHeader header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = chain.Tip.HashBlock;
                header.Bits = Target.Difficulty1;

                var chainedHeader = new ChainedHeader(header, header.GetHash(), chain.Tip);
                chain.SetTip(chainedHeader);
            }

            return chain;
        }

        private async Task WaitUntilQueueIsEmptyAsync()
        {
            int iterations = 0;

            var queue = this.blockStoreQueue.GetMemberValue("blocksQueue") as AsyncQueue<ChainedHeaderBlock>;

            while (true)
            {
                int itemsCount = ((Queue<ChainedHeaderBlock>)queue.GetMemberValue("items")).Count;

                if (itemsCount != 0)
                    await Task.Delay(100).ConfigureAwait(false);
                else
                    break;

                iterations++;

                if (iterations > 500)
                    throw new Exception("Unexpected queue processing delay!");
            }

            // For very slow environments.
            await Task.Delay(500).ConfigureAwait(false);
        }

        [Fact]
        public async Task BlockStoreInitializesTipAtHashOfLastSavedBlockAsync()
        {
            ChainedHeader initializationHeader = this.chainIndexer.Tip.Previous.Previous.Previous;
            this.repositoryTipHashAndHeight = new HashHeightPair(initializationHeader);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            Assert.Equal(initializationHeader, this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task BlockStoreRecoversToLastCommonBlockOnInitializationAsync()
        {
            this.repositoryTipHashAndHeight = new HashHeightPair(uint256.One, 1);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);

            Assert.Equal(this.chainIndexer.Genesis, this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task BatchIsSavedAfterSizeThresholdReachedAsync()
        {
            Block block = Block.Load(Encoders.Hex.DecodeData(this.testBlockHex), KnownNetworks.StratisMain.Consensus.ConsensusFactory);
            int blockSize = block.GetSerializedSize();
            this.chainState.ConsensusTip = null;

            int count = 5 * 1024 * 1024 / blockSize + 2;

            ChainIndexer longChainIndexer = this.CreateChain(count);
            this.repositoryTipHashAndHeight = new HashHeightPair(longChainIndexer.Genesis.HashBlock, 0);

            var blockStoreFlushCondition = new BlockStoreQueueFlushCondition(this.chainState, this.initialBlockDownloadState.Object);

            this.blockStoreQueue = new BlockStoreQueue(longChainIndexer, this.chainState, blockStoreFlushCondition, new StoreSettings(NodeSettings.Default(this.network)),
                this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.chainState.ConsensusTip = longChainIndexer.Tip;

            // Send all the blocks to the block store except for the last one because that will trigger batch saving because of reaching the tip.
            for (int i = 1; i < count; i++)
            {
                ChainedHeader header = longChainIndexer.GetHeader(i);

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, header));
            }

            await WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            Assert.Equal(longChainIndexer.GetHeader(count - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedOnShutdownAsync()
        {
            this.repositoryTipHashAndHeight = new HashHeightPair(this.chainIndexer.Genesis.HashBlock, 0);

            var blockStoreFlushConditionMock = new Mock<IBlockStoreQueueFlushCondition>();
            blockStoreFlushConditionMock.Setup(s => s.ShouldFlush).Returns(false);
            this.blockStoreQueue = new BlockStoreQueue(this.chainIndexer, this.chainState, blockStoreFlushConditionMock.Object, new StoreSettings(NodeSettings.Default(this.network)), this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);

            ChainedHeader lastHeader = null;

            for (int i = 1; i < this.chainIndexer.Height - 1; i++)
            {
                lastHeader = this.chainIndexer.GetHeader(i);
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chainIndexer.Genesis);
            Assert.Equal(0, this.repositorySavesCount);

            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();

            Assert.Equal(this.chainState.BlockStoreTip, lastHeader);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedWhenAtConsensusTipAsync()
        {
            this.repositoryTipHashAndHeight = new HashHeightPair(this.chainIndexer.Genesis.HashBlock, 0);

            var blockStoreFlushConditionMock = new Mock<IBlockStoreQueueFlushCondition>();
            blockStoreFlushConditionMock.Setup(s => s.ShouldFlush).Returns(false);
            this.blockStoreQueue = new BlockStoreQueue(this.chainIndexer, this.chainState, blockStoreFlushConditionMock.Object, new StoreSettings(NodeSettings.Default(this.network)), this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.chainState.ConsensusTip = this.chainIndexer.Tip;

            for (int i = 1; i <= this.chainIndexer.Height; i++)
            {
                ChainedHeader lastHeader = this.chainIndexer.GetHeader(i);
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                if (i == this.chainIndexer.Height)
                {
                    await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
                    blockStoreFlushConditionMock.Setup(s => s.ShouldFlush).Returns(true);
                }

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            // Wait for store tip to finish saving
            int counter = 0;
            if (this.chainState.BlockStoreTip != this.chainIndexer.Tip)
            {
                Assert.True(counter < 10);
                counter++;
                await Task.Delay(500);
            }

            Assert.Equal(this.chainState.BlockStoreTip, this.chainIndexer.Tip);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task ReorgedBlocksAreNotSavedAsync()
        {
            this.repositoryTipHashAndHeight = new HashHeightPair(this.chainIndexer.Genesis.HashBlock, 0);

            var blockStoreFlushConditionMock = new Mock<IBlockStoreQueueFlushCondition>();
            blockStoreFlushConditionMock.Setup(s => s.ShouldFlush).Returns(false);
            this.blockStoreQueue = new BlockStoreQueue(this.chainIndexer, this.chainState, blockStoreFlushConditionMock.Object, new StoreSettings(NodeSettings.Default(this.network)), this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);

            int reorgedChainLenght = 3;
            int realChainLenght = 6;

            // First present a short chain.
            ChainIndexer alternativeChainIndexer = CreateChain(reorgedChainLenght);
            for (int i = 1; i < alternativeChainIndexer.Height; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, alternativeChainIndexer.GetHeader(i)));
            }

            // Present second chain which has more work and reorgs blocks from genesis.
            for (int i = 1; i < realChainLenght; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, this.chainIndexer.GetHeader(i)));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chainIndexer.Genesis);
            Assert.Equal(0, this.repositorySavesCount);

            // Dispose block store to trigger save.
            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();

            // Make sure that blocks only from 2nd chain were saved.
            Assert.Equal(this.chainIndexer.GetHeader(realChainLenght - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
            Assert.Equal(realChainLenght - 1, this.repositoryTotalBlocksSaved);
        }

        /// <summary>
        /// Tests reorgs inside the batch and inside the database at the same time.
        /// </summary>
        [Fact]
        [Trait("Unstable", "True")]
        public async Task ReorgedBlocksAreDeletedFromRepositoryIfReorgDetectedAsync()
        {
            this.chainIndexer = CreateChain(1000);
            this.repositoryTipHashAndHeight = new HashHeightPair(this.chainIndexer.Genesis.HashBlock, 0);

            var blockStoreFlushCondition = new Mock<IBlockStoreQueueFlushCondition>();
            blockStoreFlushCondition.Setup(s => s.ShouldFlush).Returns(false);

            this.blockStoreQueue = new BlockStoreQueue(this.chainIndexer, this.chainState, blockStoreFlushCondition.Object, new StoreSettings(NodeSettings.Default(this.network)),
                this.blockRepositoryMock.Object, new LoggerFactory(), new Mock<INodeStats>().Object);

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.chainState.ConsensusTip = this.chainIndexer.Tip;

            // Sending 500 blocks to the queue.
            for (int i = 1; i < 500; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, this.chainIndexer.GetHeader(i)));
            }

            // Create alternative chain with fork point at 450.
            ChainedHeader prevBlock = this.chainIndexer.GetHeader(450);
            var alternativeBlocks = new List<ChainedHeader>();
            for (int i = 0; i < 100; i++)
            {
                BlockHeader header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = prevBlock.HashBlock;
                header.Bits = Target.Difficulty1;

                var chainedHeader = new ChainedHeader(header, header.GetHash(), prevBlock);
                alternativeBlocks.Add(chainedHeader);
                prevBlock = chainedHeader;
            }

            ChainedHeader savedHeader = this.chainIndexer.Tip;

            this.chainIndexer.SetTip(alternativeBlocks.Last());
            this.chainState.ConsensusTip = this.chainIndexer.Tip;

            // Present alternative chain and trigger save.
            foreach (ChainedHeader header in alternativeBlocks)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                if (header == alternativeBlocks.Last())
                    blockStoreFlushCondition.Setup(s => s.ShouldFlush).Returns(true);

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, header));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            blockStoreFlushCondition.Setup(s => s.ShouldFlush).Returns(false);

            // Make sure only longest chain is saved.
            Assert.Equal(this.chainIndexer.Tip.Height, this.repositoryTotalBlocksSaved);

            // Present a new longer chain that will reorg the repository.
            this.chainIndexer.SetTip(savedHeader);
            this.chainState.ConsensusTip = this.chainIndexer.Tip;

            for (int i = 451; i <= this.chainIndexer.Height; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.GetSerializedSize();

                if (i == this.chainIndexer.Height)
                    blockStoreFlushCondition.Setup(s => s.ShouldFlush).Returns(true);

                this.blockStoreQueue.AddToPending(new ChainedHeaderBlock(block, this.chainIndexer.GetHeader(i)));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            // Make sure chain is saved.
            Assert.Equal(this.chainIndexer.Tip.Height + alternativeBlocks.Count, this.repositoryTotalBlocksSaved);
            Assert.Equal(alternativeBlocks.Count, this.repositoryTotalBlocksDeleted);

            // Dispose block store.
            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();
        }

        [Fact]
        public async Task ThrowIfConsensusIsInitializedBeforeBlockStoreAsync()
        {
            this.repositoryTipHashAndHeight = new HashHeightPair(this.chainIndexer.Genesis.HashBlock, 0);
            this.chainState.ConsensusTip = this.chainIndexer.Tip;

            await Assert.ThrowsAsync<BlockStoreException>(async () =>
            {
                await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task RetrieveBlocksFromCacheAsync()
        {
            List<ChainedHeaderBlock> chainedHeaderBlocks = this.AddBlocksToBlockStoreQueue();

            // Try to get 10 random blocks.
            for (int i = 0; i < 10; i++)
            {
                int blockIndex = this.random.Next(0, chainedHeaderBlocks.Count);

                Block blockToFind = chainedHeaderBlocks[blockIndex].Block;

                Block foundBlock = await this.blockStoreQueue.GetBlockAsync(blockToFind.GetHash());
                Assert.Equal(foundBlock, blockToFind);
            }
        }

        [Fact]
        public async Task RetrieveTransactionByIdFromCacheReturnsNullWhenNotIndexedAsync()
        {
            List<ChainedHeaderBlock> chainedHeaderBlocks = this.AddBlocksToBlockStoreQueue();

            // Try to get 10 random transactions.
            for (int i = 0; i < 10; i++)
            {
                int blockIndex = this.random.Next(0, chainedHeaderBlocks.Count);

                Transaction txToFind = chainedHeaderBlocks[blockIndex].Block.Transactions.First();

                Transaction foundTx = await this.blockStoreQueue.GetTransactionByIdAsync(txToFind.GetHash());
                Assert.Null(foundTx);
            }
        }

        [Fact]
        public async Task RetrieveBlockIdByTxIdFromCacheAsync()
        {
            List<ChainedHeaderBlock> chainedHeaderBlocks = this.AddBlocksToBlockStoreQueue();

            // Try to get 10 random block ids.
            for (int i = 0; i < 10; i++)
            {
                int blockIndex = this.random.Next(0, chainedHeaderBlocks.Count);

                Transaction txToFind = chainedHeaderBlocks[blockIndex].Block.Transactions.First();

                uint256 foundBlockHash = await this.blockStoreQueue.GetBlockIdByTransactionIdAsync(txToFind.GetHash());
                Assert.Equal(chainedHeaderBlocks[blockIndex].Block.GetHash(), foundBlockHash);
            }
        }

        private List<ChainedHeaderBlock> AddBlocksToBlockStoreQueue(int blocksCount = 500)
        {
            var chainedHeaderBlocks = new List<ChainedHeaderBlock>(blocksCount);

            for (int i = 0; i < blocksCount; i++)
            {
                Block block = TransactionsHelper.CreateDummyBlockWithTransaction(this.network, this.chainIndexer.Tip);

                var header = new ChainedHeader(block.Header, block.GetHash(), this.chainIndexer.Tip);

                this.chainIndexer.SetTip(header);
                chainedHeaderBlocks.Add(new ChainedHeaderBlock(block, header));
            }

            foreach (ChainedHeaderBlock chainedHeaderBlock in chainedHeaderBlocks)
                this.blockStoreQueue.AddToPending(chainedHeaderBlock);

            return chainedHeaderBlocks;
        }
    }
}