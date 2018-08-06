using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletSyncManagerTest : LogsTestBase
    {
        private ConcurrentChain chain;
        private Mock<IWalletManager> walletManager;
        private Mock<IBlockStore> blockStore;
        private Mock<INodeLifetime> nodeLifetime;
        private StoreSettings storeSettings;
        private Mock<IAsyncLoopFactory> asyncLoopFactory;

        public WalletSyncManagerTest()
        {
            this.storeSettings = new StoreSettings(new NodeSettings(KnownNetworks.StratisMain));
            this.chain = new ConcurrentChain(KnownNetworks.StratisMain);
            this.walletManager = new Mock<IWalletManager>();
            this.blockStore = new Mock<IBlockStore>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
        }

        [Fact]
        public void Start_HavingPrunedStoreSetting_ThrowsWalletException()
        {
            this.storeSettings.Prune = true;

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.Start();
            });
        }

        [Fact]
        public void Start_BlockOnChain_DoesNotReorgWalletManager()
        {
            this.storeSettings.Prune = false;
            this.chain = WalletTestsHelpers.PrepareChainWithBlock();
            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chain.Tip.Header.GetHash());

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.Start();

            this.walletManager.Verify(w => w.GetFirstWalletBlockLocator(), Times.Exactly(0));
            this.walletManager.Verify(w => w.RemoveBlocks(It.IsAny<ChainedHeader>()), Times.Exactly(0));
        }

        [Fact]
        public void Start_BlockNotChain_ReorgsWalletManagerUsingWallet()
        {
            this.storeSettings.Prune = false;
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(5, KnownNetworks.StratisMain);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(125)); // try to load non-existing block to get chain to return null.

            ChainedHeader forkBlock = this.chain.GetBlock(3); // use a block as the fork to recover to.
            uint256 forkBlockHash = forkBlock.Header.GetHash();
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new Collection<uint256> { forkBlockHash });

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.Start();

            // verify the walletmanager is reorged using the fork block and it's tip is set to it.
            this.walletManager.Verify(w => w.RemoveBlocks(It.Is<ChainedHeader>(c => c.Header.GetHash() == forkBlockHash)));
            this.walletManager.VerifySet(w => w.WalletTipHash = forkBlockHash);
            Assert.Equal(walletSyncManager.WalletTip.HashBlock.ToString(), forkBlock.HashBlock.ToString());
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is the same as the <see cref="WalletSyncManager.WalletTip"/> pass it directly to the <see cref="WalletManager"/>
        /// and set it as the new WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashSameAsWalletTip_PassesBlockToManagerWithoutReorg()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);

            this.chain = result.Chain;

            List<Block> blocks = result.Blocks;

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, new AsyncLoopFactory(this.LoggerFactory.Object));

            walletSyncManager.SetWalletTip(this.chain.GetBlock(3));

            this.walletManager.Setup(w => w.WalletTipHash).Returns(walletSyncManager.WalletTip.HashBlock);

            Block blockToProcess = blocks[3];
            walletSyncManager.ProcessBlock(blockToProcess); //4th block in the list has same prevhash as which is loaded

            // Start() will execute private methods asyncLoopFactory will run() ProcessBlockLoopAsync(), so need to wait some time.
            walletSyncManager.Start();

            uint256 expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(), $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            walletSyncManager.Stop();

            this.walletManager.Verify(w => w.ProcessBlock(blockToProcess, this.chain.GetBlock(4)));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is not on the best chain
        /// look for the point at which the chain forked and remove blocks after that fork point from the <see cref="WalletManager"/>.
        /// After removing those blocks use the <see cref="BlockStoreCache"/> to retrieve blocks on the best chain and use those to catchup the WalletManager.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockNotOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ConcurrentChain LeftChain, ConcurrentChain RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks) result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, KnownNetworks.StratisMain, 2);
            
            // left side chain containing the 'old' fork.
            ConcurrentChain leftChain = result.LeftChain;

            // right side chain containing the 'new' fork. Work on this.
            this.chain = result.RightChain;

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, new AsyncLoopFactory(this.LoggerFactory.Object));

            // setup blockstorecache to return blocks on the chain.
            this.blockStore.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
                .ReturnsAsync((uint256 hashblock) =>
                {
                    return result.LeftForkBlocks.Union(result.RightForkBlocks).Single(b => b.GetHash() == hashblock);
                });

            // set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            walletSyncManager.SetWalletTip(leftChain.GetBlock(result.LeftForkBlocks[3].Header.GetHash()));
            this.walletManager.Setup(w => w.WalletTipHash).Returns(walletSyncManager.WalletTip.HashBlock);

            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator()).Returns(new List<uint256>{ this.chain.GetBlock(2).HashBlock });

            //process 5th block from the right side of the fork in the list does not have same prevhash as which is loaded.
            Block blockToProcess = result.RightForkBlocks[4];
            walletSyncManager.ProcessBlock(blockToProcess);

            // Start() will execute private methods asyncLoopFactory will run() ProcessBlockLoopAsync(), so need to wait some time.
            walletSyncManager.Start();

            uint256 expectedBlockHash = this.chain.GetBlock(5).Header.GetHash();
            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(), $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            walletSyncManager.Stop();

            // walletmanager removes all blocks up to the fork.
            this.walletManager.Verify(w => w.RemoveBlocks(ExpectChainedBlock(this.chain.GetBlock(2))));

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))));
            // height 5
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[4]), ExpectChainedBlock(this.chain.GetBlock(5))), Times.Exactly(1));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is on the best chain
        /// see which blocks are missing and retrieve blocks from the <see cref="BlockStoreCache"/> to catchup the <see cref="WalletManager"/>.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock__BlockOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);

            this.chain = result.Chain;

            List<Block> blocks = result.Blocks;

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, new AsyncLoopFactory(this.LoggerFactory.Object));

            // setup blockstorecache to return blocks on the chain.
            this.blockStore.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
                .ReturnsAsync((uint256 hashblock) =>
                {
                    return blocks.Single(b => b.GetHash() == hashblock);
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chain.GetBlock(2));
            this.walletManager.Setup(w => w.WalletTipHash).Returns(walletSyncManager.WalletTip.HashBlock);

            //process 4th block in the list does not have same prevhash as which is loaded
            walletSyncManager.ProcessBlock(blocks[3]);

            // Start() will execute private methods asyncLoopFactory will run() ProcessBlockLoopAsync(), so need to wait some time.
            walletSyncManager.Start();

            uint256 expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(), $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            walletSyncManager.Stop();

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))), Times.Exactly(2));
        }

        /// <summary>
        /// When using the <see cref="BlockStoreCache"/> to catchup on the <see cref="WalletManager"/> and the <see cref="Block"/> is not in the BlockStoreCache yet try to wait until it arrives.
        /// If it does use it to catchup the WalletManager.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockArrivesLateInBlockStoreCache_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);

            this.chain = result.Chain;

            List<Block> blocks = result.Blocks;

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, new AsyncLoopFactory(this.LoggerFactory.Object));

            var blockEmptyCounters = new Dictionary<uint256, int>();

            // setup blockstorecache to return blocks on the chain but postpone by 3 rounds for each block.
            this.blockStore.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
                .ReturnsAsync((uint256 hashblock) =>
                {
                    if (!blockEmptyCounters.ContainsKey(hashblock))
                    {
                        blockEmptyCounters.Add(hashblock, 0);
                    }

                    if (blockEmptyCounters[hashblock] < 3)
                    {
                        blockEmptyCounters[hashblock] += 1;
                        return null;
                    }
                    else
                    {
                        return blocks.Single(b => b.GetHash() == hashblock);
                    }
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chain.GetBlock(2));
            
            this.walletManager.Setup(w => w.WalletTipHash).Returns(walletSyncManager.WalletTip.HashBlock);

            //process 4th block in the list does not have same previous hash as which is loaded
            walletSyncManager.ProcessBlock(blocks[3]);
            
            // Start() will execute private methods asyncLoopFactory will run() ProcessBlockLoopAsync(), so need to wait some time.
            walletSyncManager.Start();

            uint256 expectedBlockHash = this.chain.GetBlock(5).Header.GetHash();
            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(), $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            walletSyncManager.Stop();

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))));
        }

        [Fact]
        public void ProcessTransaction_CallsWalletManager()
        {
            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
               this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            var transaction = new Transaction
            {
                Version = 15
            };

            walletSyncManager.ProcessTransaction(transaction);

            this.walletManager.Verify(w => w.ProcessTransaction(transaction, null, null, true));
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the closest <see cref="Block"/> to the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateMatchingBlocksOnChain_UpdatesUsingClosestBlock()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.SyncFromDate(this.chain.GetBlock(3).Header.BlockTime.DateTime.AddDays(2));

            uint256 expectedHash = this.chain.GetBlock(3).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the first <see cref="Block"/> if there is no block near the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateNotMatchingBlocksOnChain_UpdatesUsingFirstBlock()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.SyncFromDate(new System.DateTime(1900, 1, 1)); // date before any block.

            uint256 expectedHash = this.chain.GetBlock(1).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the genesis <see cref="Block"/> if there is no block on the chain.
        /// </summary>
        [Fact]
        public void SyncFromDate_EmptyChain_UpdateUsingGenesisBlock()
        {
            this.chain = new ConcurrentChain(KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.SyncFromDate(new System.DateTime(1900, 1, 1)); // date before any block.

            uint256 expectedHash = this.chain.Genesis.HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        [Fact]
        public void SyncFromHeight_BlockWithHeightOnChain_UpdatesWalletTipOnWalletAndWalletSyncManagers()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            walletSyncManager.SyncFromHeight(2);

            uint256 expectedHash = this.chain.GetBlock(2).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        [Fact]
        public void SyncFromHeight_NoBlockWithGivenHeightOnChain_ThrowsWalletException()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(1, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.SyncFromHeight(2);
            });
        }

        [Fact]
        public void ProcessBlock_Sync_Queued_Blocks_Are_Processed_By_WalletManager()
        {
            const int blockCount = 5;
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(blockCount, KnownNetworks.StratisMain);

            this.chain = result.Chain;

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.nodeLifetime.Object, new AsyncLoopFactory(this.LoggerFactory.Object));

            walletSyncManager.SetWalletTip(this.chain.GetBlock(0));
            this.walletManager.Setup(w => w.WalletTipHash).Returns(walletSyncManager.WalletTip.HashBlock);
            
            foreach (Block block in result.Blocks)
                walletSyncManager.ProcessBlock(block);

            walletSyncManager.Start();

            uint256 expectedBlockHash = this.chain.GetBlock(5).Header.GetHash();

            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(), $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            walletSyncManager.Stop();

            this.walletManager.Verify(w => w.ProcessBlock(It.IsAny<Block>(), It.IsAny<ChainedHeader>()), Times.Exactly(blockCount));
        }

        private static ChainedHeader ExpectChainedBlock(ChainedHeader block)
        {
            return It.Is<ChainedHeader>(c => c.Header.GetHash() == block.Header.GetHash());
        }

        private static Block ExpectBlock(Block block)
        {
            return It.Is<Block>(b => b.GetHash() == block.GetHash());
        }

        private class WalletSyncManagerOverride : WalletSyncManager
        {
            public WalletSyncManagerOverride(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
                Network network, IBlockStore blockStore, StoreSettings storeSettings, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
                : base(loggerFactory, walletManager, chain, network, blockStore, storeSettings, nodeLifetime, asyncLoopFactory)
            {
            }

            public void SetWalletTip(ChainedHeader tip)
            {
                this.walletTip = tip;
            }
        }

        private static void WaitLoop(Func<bool> act, string failureReason, int millisecondsTimeout = 50)
        {
            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while (!act())
            {
                try
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(millisecondsTimeout);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }
    }
}
