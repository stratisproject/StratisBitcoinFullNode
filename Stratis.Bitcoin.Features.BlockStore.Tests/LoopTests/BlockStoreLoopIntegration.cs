using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.Features.BlockStore.Tests.LoopTests
{
    public sealed class BlockStoreLoopIntegration : BlockStoreLoopStepBaseTest
    {
        /// <summary>
        /// Tests the block store loop step with a concrete implementation of BlockRepository.
        /// </summary>
        [Fact]
        public void CheckNextChainedBlockExists_WithNextChainedBlock_Exists_SetStoreTipAndBlockHash()
        {
            var blocks = CreateBlocks(5);

            using (var fluent = new FluentBlockStoreLoop())
            {
                fluent.WithConcreteRepository(Path.Combine(AppContext.BaseDirectory, "BlockStore", "CheckNextChainedBlockExists_Integration"));

                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 4 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(3));

                // Create the last chained block without appending to the chain
                var block03 = chain.GetBlock(blocks[3].GetHash());
                var block04 = new ChainedBlock(blocks[4].Header, blocks[4].Header.GetHash(), block03);

                fluent.Create(chain);

                Assert.Null(fluent.Loop.StoreTip);

                var nextChainedBlock = block04;
                var checkExistsStep = new CheckNextChainedBlockExistStep(fluent.Loop, this.loggerFactory);
                checkExistsStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(fluent.Loop.StoreTip.Header.GetHash(), block04.Header.GetHash());
                Assert.Equal(fluent.Loop.BlockRepository.BlockHash, block04.Header.GetHash());
            }
        }

        [Fact]
        public void ReorganiseBlockRepository_WithBlockRepositoryAndChainOutofSync_ReorganiseBlocks()
        {
            var blocks = CreateBlocks(15);

            using (var fluent = new FluentBlockStoreLoop())
            {
                fluent.WithConcreteRepository(Path.Combine(AppContext.BaseDirectory, "BlockStore", "ReorganiseBlockRepository_Integration"));

                // Push 15 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Last().GetHash(), blocks).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create the last 5 chained blocks without appending to the chain
                var block9 = chain.GetBlock(blocks[9].Header.GetHash());
                var block10 = new ChainedBlock(blocks[10].Header, blocks[10].Header.GetHash(), block9);
                var block11 = new ChainedBlock(blocks[11].Header, blocks[11].Header.GetHash(), block10);
                var block12 = new ChainedBlock(blocks[12].Header, blocks[12].Header.GetHash(), block11);
                var block13 = new ChainedBlock(blocks[13].Header, blocks[13].Header.GetHash(), block12);
                var block14 = new ChainedBlock(blocks[14].Header, blocks[14].Header.GetHash(), block13);

                fluent.Create(chain);
                fluent.Loop.SetStoreTip(block14);

                Assert.Equal(fluent.Loop.StoreTip.Header.GetHash(), block14.Header.GetHash());
                Assert.Equal(fluent.Loop.BlockRepository.BlockHash, block14.Header.GetHash());

                var nextChainedBlock = block10;
                var reorganiseStep = new ReorganiseBlockRepositoryStep(fluent.Loop, this.loggerFactory);
                reorganiseStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(fluent.Loop.StoreTip.Header.GetHash(), block10.Previous.Header.GetHash());
                Assert.Equal(fluent.Loop.BlockRepository.BlockHash, block10.Previous.Header.GetHash());
            }
        }

        [Fact]
        public void ProcessPendingStorage_WithPendingBlocks_PushToRepoBeforeDownloadingNewBlocks()
        {
            var blocks = CreateBlocks(15);

            using (var fluent = new FluentBlockStoreLoop().AsIBD())
            {
                fluent.WithConcreteRepository(Path.Combine(AppContext.BaseDirectory, "BlockStore", "ProcessPendingStorage_Integration"));

                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 15 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(14));

                // Create block store loop
                fluent.Create(chain);

                //Set the store's tip
                fluent.Loop.SetStoreTip(fluent.Loop.Chain.GetBlock(blocks.Take(5).Last().GetHash()));

                // Add chained blocks 5 - 14 to PendingStorage
                AddBlockToPendingStorage(fluent.Loop, blocks[5]);
                AddBlockToPendingStorage(fluent.Loop, blocks[6]);
                AddBlockToPendingStorage(fluent.Loop, blocks[7]);
                AddBlockToPendingStorage(fluent.Loop, blocks[8]);
                AddBlockToPendingStorage(fluent.Loop, blocks[9]);
                AddBlockToPendingStorage(fluent.Loop, blocks[10]);
                AddBlockToPendingStorage(fluent.Loop, blocks[11]);
                AddBlockToPendingStorage(fluent.Loop, blocks[12]);
                AddBlockToPendingStorage(fluent.Loop, blocks[13]);
                AddBlockToPendingStorage(fluent.Loop, blocks[14]);

                //Start processing pending blocks from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var processPendingStorageStep = new ProcessPendingStorageStep(fluent.Loop, this.loggerFactory);
                processPendingStorageStep.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[14].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[14].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }

        [Fact]
        public void DownloadBlockStep_WithNewBlocksToDownload_DownloadBlocksAndPushToRepo()
        {
            var blocks = CreateBlocks(10);

            using (var fluent = new FluentBlockStoreLoop())
            {
                fluent.WithConcreteRepository(Path.Combine(AppContext.BaseDirectory, "BlockStore", "DownloadBlocks_Integration"));

                // Push 5 blocks to the repository
                fluent.BlockRepository.PutAsync(blocks.Take(5).Last().GetHash(), blocks.Take(5).ToList()).GetAwaiter().GetResult();

                // The chain has 10 blocks appended
                var chain = new ConcurrentChain(blocks[0].Header);
                AppendBlocksToChain(chain, blocks.Skip(1).Take(9));

                // Create block store loop
                fluent.Create(chain);

                // Push blocks 5 - 9 to the downloaded blocks collection
                fluent.Loop.BlockPuller.InjectBlock(blocks[5].GetHash(), new DownloadedBlock() { Length = blocks[5].GetSerializedSize(), Block = blocks[5] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[6].GetHash(), new DownloadedBlock() { Length = blocks[6].GetSerializedSize(), Block = blocks[6] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[7].GetHash(), new DownloadedBlock() { Length = blocks[7].GetSerializedSize(), Block = blocks[7] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[8].GetHash(), new DownloadedBlock() { Length = blocks[8].GetSerializedSize(), Block = blocks[8] }, new CancellationToken());
                fluent.Loop.BlockPuller.InjectBlock(blocks[9].GetHash(), new DownloadedBlock() { Length = blocks[9].GetSerializedSize(), Block = blocks[9] }, new CancellationToken());

                // Start processing blocks to download from block 5
                var nextChainedBlock = fluent.Loop.Chain.GetBlock(blocks[5].GetHash());

                var step = new DownloadBlockStep(fluent.Loop, this.loggerFactory, DateTimeProvider.Default);
                step.ExecuteAsync(nextChainedBlock, new CancellationToken(), false).GetAwaiter().GetResult();

                Assert.Equal(blocks[9].GetHash(), fluent.Loop.BlockRepository.BlockHash);
                Assert.Equal(blocks[9].GetHash(), fluent.Loop.StoreTip.HashBlock);
            }
        }
    }
}