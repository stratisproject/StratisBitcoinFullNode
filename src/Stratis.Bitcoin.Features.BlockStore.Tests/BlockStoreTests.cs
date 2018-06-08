﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;
using Moq;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreTests
    {
        private BlockStoreQueue blockStoreQueue;
        private readonly IBlockRepository repository;
        private readonly IChainState chainState;
        private readonly NodeLifetime nodeLifetime;
        private ConcurrentChain chain;

        private uint256 repositoryBlockHash;
        private int repositorySavesCount = 0;
        private int repositoryTotalBlocksSaved = 0;
        private int repositoryTotalBlocksDeleted = 0;

        private Dictionary<uint256, Block> listOfSavedBlocks;

        private ChainedHeader chainStateBlockStoreTip;

        private ChainedHeader consensusTip;

        private string testBlockHex = "07000000af72d939050259913e440b23bee62e3b9604129ec8424d265a6ee4916e060000a5a2cbad28617657336403daf202b797bfc4b9c5cfc65a258f32ec33ec9ad485314ea957ffff0f1e812b07000101000000184ea957010000000000000000000000000000000000000000000000000000000000000000ffffffff03510101ffffffff010084d717000000001976a9140099e795d9ee809dc74dce32c79d26db0265072488ac0000000000";

        public BlockStoreTests()
        {
            this.listOfSavedBlocks = new Dictionary<uint256, Block>();
            this.listOfSavedBlocks.Add(uint256.One, Block.Parse(this.testBlockHex, Network.StratisMain));

            this.chain = this.CreateChain(10);
            this.consensusTip = null;
            this.nodeLifetime = new NodeLifetime();

            var blockRepositoryMock = new Mock<IBlockRepository>();
            blockRepositoryMock.Setup(x => x.PutAsync(It.IsAny<uint256>(), It.IsAny<List<Block>>()))
                .Returns((uint256 nextBlockHash, List<Block> blocks) =>
            {
                this.repositoryBlockHash = nextBlockHash;
                this.repositorySavesCount++;
                this.repositoryTotalBlocksSaved += blocks.Count;
                return Task.CompletedTask;
            });

            blockRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<uint256>(), It.IsAny<List<uint256>>()))
                .Returns((uint256 nextBlockHash, List<uint256> blocks) =>
            {
                this.repositoryTotalBlocksDeleted += blocks.Count;
                return Task.CompletedTask;
            });

            blockRepositoryMock.Setup(x => x.GetAsync(It.IsAny<uint256>()))
                .Returns((uint256 hash) =>
            {
                Block block = null;

                if (this.listOfSavedBlocks.ContainsKey(hash))
                    block = this.listOfSavedBlocks[hash];

                return Task.FromResult(block);
            });

            blockRepositoryMock.Setup(x => x.BlockHash).Returns(() =>
            {
                return this.repositoryBlockHash;
            });

            this.repository = blockRepositoryMock.Object;

            var chainStateMoq = new Mock<IChainState>();
            chainStateMoq.Setup(x => x.ConsensusTip).Returns(() => this.consensusTip);
            chainStateMoq.SetupProperty(x => x.BlockStoreTip, this.chainStateBlockStoreTip);

            this.chainState = chainStateMoq.Object;

            this.blockStoreQueue = new BlockStoreQueue(this.repository, this.chain, this.chainState, new StoreSettings(), this.nodeLifetime, new LoggerFactory());
        }

        private ConcurrentChain CreateChain(int blocksCount)
        {
            var chain = new ConcurrentChain(Network.StratisMain);

            for (int i = 0; i < blocksCount; i++)
            {
                var header = new BlockHeader()
                {
                    Nonce = RandomUtils.GetUInt32(),
                    HashPrevBlock = chain.Tip.HashBlock,
                    Bits = Target.Difficulty1
                };
                
                var chainedHeader = new ChainedHeader(header, header.GetHash(), chain.Tip);

                chain.SetTip(chainedHeader);
            }

            return chain;
        }

        private async Task WaitUntilQueueIsEmptyAsync()
        {
            int iterations = 0;

            var queue = this.blockStoreQueue.GetMemberValue("blocksQueue") as AsyncQueue<BlockPair>;
            
            while (true)
            {
                int itemsCount = ((Queue<BlockPair>)queue.GetMemberValue("items")).Count;

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
            ChainedHeader initializationHeader = this.chain.Tip.Previous.Previous.Previous;
            this.repositoryBlockHash = initializationHeader.HashBlock;

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            Assert.Equal(initializationHeader, this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task BlockStoreRecoversToLastCommonBlockOnInitializationAsync()
        {
            this.repositoryBlockHash = uint256.One;

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            
            Assert.Equal(this.chain.Genesis, this.chainState.BlockStoreTip);
        }

        [Fact]
        public async Task BatchIsSavedAfterSizeThresholdReachedAsync()
        {
            Block block = Block.Load(Encoders.Hex.DecodeData(this.testBlockHex), Network.StratisMain);
            int blockSize = block.GetSerializedSize();
            this.consensusTip = null;

            int count = BlockStoreQueue.BatchThresholdSizeBytes / blockSize + 2;

            ConcurrentChain longChain = this.CreateChain(count);
            this.repositoryBlockHash = longChain.Genesis.HashBlock;

            this.blockStoreQueue = new BlockStoreQueue(this.repository, longChain, this.chainState, new StoreSettings(), new NodeLifetime(), new LoggerFactory());

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.consensusTip = longChain.Tip;

            // Send all the blocks to the block store except for the last one because that will trigger batch saving because of reaching the tip.
            for (int i = 1; i < count; i++)
            {
                ChainedHeader header = longChain.GetBlock(i);

                this.blockStoreQueue.AddToPending(new BlockPair(block, header));
            }
            
            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            Assert.Equal(longChain.GetBlock(count - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedOnShutdownAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);

            ChainedHeader lastHeader = null;

            for (int i = 1; i < this.chain.Height - 1; i++)
            {
                lastHeader = this.chain.GetBlock(i);
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Genesis);
            Assert.Equal(0, this.repositorySavesCount);
            
            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();

            Assert.Equal(this.chainState.BlockStoreTip, lastHeader);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task BatchIsSavedWhenAtConsensusTipAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.consensusTip = this.chain.Tip;

            for (int i = 1; i <= this.chain.Height; i++)
            {
                ChainedHeader lastHeader = this.chain.GetBlock(i);
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), lastHeader));
            }

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            
            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Tip);
            Assert.Equal(1, this.repositorySavesCount);
        }

        [Fact]
        public async Task ReorgedBlocksAreNotSavedAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);

            int reorgedChainLenght = 3;
            int realChainLenght = 6;
            
            // First present a short chain.
            ConcurrentChain alternativeChain = this.CreateChain(reorgedChainLenght);
            for (int i = 1; i < alternativeChain.Height; i++)
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), alternativeChain.GetBlock(i)));

            // Present second chain which has more work and reorgs blocks from genesis. 
            for (int i = 1; i < realChainLenght; i++)
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));
            
            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            Assert.Equal(this.chainState.BlockStoreTip, this.chain.Genesis);
            Assert.Equal(0, this.repositorySavesCount);

            // Dispose block store to trigger save.
            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();
            
            // Make sure that blocks only from 2nd chain were saved.
            Assert.Equal(this.chain.GetBlock(realChainLenght - 1), this.chainState.BlockStoreTip);
            Assert.Equal(1, this.repositorySavesCount);
            Assert.Equal(realChainLenght - 1, this.repositoryTotalBlocksSaved);
        }

        /// <summary>
        /// Tests reorgs inside the batch and inside the database at the same time.
        /// </summary>
        [Fact]
        public async Task ReorgedBlocksAreDeletedFromRepositoryIfReorgDetectedAsync()
        {
            this.chain = this.CreateChain(1000);
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;

            this.blockStoreQueue = new BlockStoreQueue(this.repository, this.chain, this.chainState, new StoreSettings(), this.nodeLifetime, new LoggerFactory());

            await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            this.consensusTip = this.chain.Tip;

            // Sending 500 blocks to the queue.
            for (int i = 1; i < 500; i++)
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));
            
            // Create alternative chain with fork point at 450.
            ChainedHeader prevBlock = this.chain.GetBlock(450);
            var alternativeBlocks = new List<ChainedHeader>();
            for (int i = 0; i < 100; i++)
            {
                var header = new BlockHeader()
                {
                    Nonce = RandomUtils.GetUInt32(),
                    HashPrevBlock = prevBlock.HashBlock,
                    Bits = Target.Difficulty1
                };

                var chainedHeader = new ChainedHeader(header, header.GetHash(), prevBlock);
                alternativeBlocks.Add(chainedHeader);
                prevBlock = chainedHeader;
            }

            ChainedHeader savedHeader = this.chain.Tip;

            this.chain.SetTip(alternativeBlocks.Last());
            this.consensusTip = this.chain.Tip;

            // Present alternative chain and trigger save.
            foreach (ChainedHeader header in alternativeBlocks)
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), header));

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);

            // Make sure only longest chain is saved.
            Assert.Equal(1, this.repositorySavesCount);
            Assert.Equal(this.chain.Tip.Height, this.repositoryTotalBlocksSaved);

            // Present a new longer chain that will reorg the repository.
            this.chain.SetTip(savedHeader);
            this.consensusTip = this.chain.Tip;

            for (int i = 451; i <= this.chain.Height; i++)
                this.blockStoreQueue.AddToPending(new BlockPair(new Block(), this.chain.GetBlock(i)));

            await this.WaitUntilQueueIsEmptyAsync().ConfigureAwait(false);
            
            // Make sure chain is saved.
            Assert.Equal(2, this.repositorySavesCount);
            Assert.Equal(this.chain.Tip.Height + alternativeBlocks.Count, this.repositoryTotalBlocksSaved);
            Assert.Equal(alternativeBlocks.Count, this.repositoryTotalBlocksDeleted);

            // Dispose block store.
            this.nodeLifetime.StopApplication();
            this.blockStoreQueue.Dispose();
        }

        [Fact]
        public async Task ThrowIfConsensusIsInitializedBeforeBlockStoreAsync()
        {
            this.repositoryBlockHash = this.chain.Genesis.HashBlock;
            this.consensusTip = this.chain.Tip;

            await Assert.ThrowsAsync<BlockStoreException>(async () =>
            {
                await this.blockStoreQueue.InitializeAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
