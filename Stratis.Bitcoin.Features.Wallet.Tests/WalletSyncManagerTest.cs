using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Moq;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletSyncManagerTest : LogsTestBase
    {
        private ConcurrentChain chain;
        private Mock<IWalletManager> walletManager;
        private Mock<IBlockStoreCache> blockStoreCache;
        private Mock<INodeLifetime> nodeLifetime;
        private StoreSettings storeSettings;

        public WalletSyncManagerTest()
        {
            this.storeSettings = new StoreSettings()
            {
                Prune = false
            };

            this.chain = new ConcurrentChain(Network.StratisMain);
            this.walletManager = new Mock<IWalletManager>();
            this.blockStoreCache = new Mock<IBlockStoreCache>();
            this.nodeLifetime = new Mock<INodeLifetime>();
        }

        [Fact]
        public void Initialize_HavingPrunedStoreSetting_ThrowsWalletException()
        {
            this.storeSettings.Prune = true;

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.Initialize().Wait();
            });
        }

        [Fact]
        public void Initialize_BlockOnChain_DoesNotReorgWalletManager()
        {
            this.storeSettings.Prune = false;
            this.chain = WalletTestsHelpers.PrepareChainWithBlock();
            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chain.Tip.Header.GetHash());

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);

            var task = walletSyncManager.Initialize();
            task.Wait();

            this.walletManager.Verify(w => w.GetFirstWalletBlockLocator(), Times.Exactly(0));
            this.walletManager.Verify(w => w.RemoveBlocks(It.IsAny<ChainedBlock>()), Times.Exactly(0));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void Initialize_BlockNotChain_ReorgsWalletManagerUsingWallet()
        {
            this.storeSettings.Prune = false;
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(5, Network.StratisMain);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(125)); // try to load non-existing block to get chain to return null.

            var forkBlock = this.chain.GetBlock(3); // use a block as the fork to recover to.
            var forkBlockHash = forkBlock.Header.GetHash();
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new Collection<uint256> { forkBlockHash });

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);

            var task = walletSyncManager.Initialize();
            task.Wait();

            // verify the walletmanager is reorged using the fork block and it's tip is set to it.
            this.walletManager.Verify(w => w.RemoveBlocks(It.Is<ChainedBlock>(c => c.Header.GetHash() == forkBlockHash)));
            this.walletManager.VerifySet(w => w.WalletTipHash = forkBlockHash);
            Assert.Equal(walletSyncManager.WalletTip.HashBlock.ToString(), forkBlock.HashBlock.ToString());
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashSameAsWalletTip_PassesBlockToManagerWithoutReorg()
        {
            var result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, Network.StratisMain);
            this.chain = result.Chain;
            var blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);
            walletSyncManager.SetWalletTip(this.chain.GetBlock(3));

            var blockToProcess = blocks[3];
            walletSyncManager.ProcessBlock(blockToProcess); //4th block in the list has same prevhash as which is loaded

            var expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            Assert.Equal(expectedBlockHash, walletSyncManager.WalletTip.Header.GetHash());
            this.walletManager.Verify(w => w.ProcessBlock(It.Is<Block>(b => b.GetHash() == blockToProcess.GetHash()), It.Is<ChainedBlock>(c => c.Header.GetHash() == expectedBlockHash)));
        }

        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashNotSameAsWalletTip_WalletTipNotOnBestChain_RemovesBlocksAfterForkOnOldChainFromWalletManager_CatchUpWalletManagerUsingBlockStoreCache()
        {
            var result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, Network.StratisMain, 2);
            // left side chain containing the 'old' fork.
            var leftChain = result.LeftChain;
            // right side chain containing the 'new' fork. Work on this.
            this.chain = result.RightChain;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);
            // setup blockstorecache to return blocks on the chain.
            this.blockStoreCache.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
                .ReturnsAsync((uint256 hashblock) =>
                {
                    return result.LeftForkBlocks.Union(result.RightForkBlocks).Where(b => b.GetHash() == hashblock).Single();
                });

            // set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            walletSyncManager.SetWalletTip(leftChain.GetBlock(result.LeftForkBlocks[3].Header.GetHash()));
            //process 5th block from the right side of the fork in the list does not have same prevhash as which is loaded.
            var blockToProcess = result.RightForkBlocks[4];
            walletSyncManager.ProcessBlock(blockToProcess);

            // walletmanager removes all blocks up to the fork.
            this.walletManager.Verify(w => w.RemoveBlocks(ExpectChainedBlock(this.chain.GetBlock(2))));
            var expectedBlockHash = this.chain.GetBlock(5).Header.GetHash();
            Assert.Equal(expectedBlockHash, walletSyncManager.WalletTip.Header.GetHash());

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))));
            // height 5
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[4]), ExpectChainedBlock(this.chain.GetBlock(5))), Times.Exactly(2));
        }

        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashNotSameAsWalletTip_WalletTipOnBestChain_BlockInBlockStoreCache_CatchUpWalletManagerUsingBlockStoreCache()
        {
            var result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, Network.StratisMain);
            this.chain = result.Chain;
            var blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);
            // setup blockstorecache to return blocks on the chain.
            this.blockStoreCache.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
                .ReturnsAsync((uint256 hashblock) =>
                {
                    return blocks.Where(b => b.GetHash() == hashblock).Single();
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chain.GetBlock(2));
            //process 4th block in the list does not have same prevhash as which is loaded
            var blockToProcess = blocks[3];
            walletSyncManager.ProcessBlock(blockToProcess); 

            var expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            Assert.Equal(expectedBlockHash, walletSyncManager.WalletTip.Header.GetHash());
            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))), Times.Exactly(2));
        }

        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashNotSameAsWalletTip_WalletTipOnBestChain_BlockArrivesLateInBlockStoreCache_CatchUpWalletManagerUsingBlockStoreCache()
        {
            var result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, Network.StratisMain);
            this.chain = result.Chain;
            var blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, Network.StratisMain,
                this.blockStoreCache.Object, this.storeSettings, this.nodeLifetime.Object);
            var blockEmptyCounters = new Dictionary<uint256, int>();
            // setup blockstorecache to return blocks on the chain but postpone by 3 rounds for each block.
            this.blockStoreCache.Setup(b => b.GetBlockAsync(It.IsAny<uint256>()))
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
                        return blocks.Where(b => b.GetHash() == hashblock).Single();
                    }
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chain.GetBlock(2)); 
            //process 4th block in the list  does not have same prevhash as which is loaded
            var blockToProcess = blocks[3];
            walletSyncManager.ProcessBlock(blockToProcess);

            var expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            Assert.Equal(expectedBlockHash, walletSyncManager.WalletTip.Header.GetHash());
            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chain.GetBlock(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))), Times.Exactly(2));
        }

        private static ChainedBlock ExpectChainedBlock(ChainedBlock block)
        {
            return It.Is<ChainedBlock>(c => c.Header.GetHash() == block.Header.GetHash());
        }

        private static Block ExpectBlock(Block block)
        {
            return It.Is<Block>(b => b.GetHash() == block.GetHash());
        }

        private class WalletSyncManagerOverride : WalletSyncManager
        {
            public WalletSyncManagerOverride(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
                Network network, IBlockStoreCache blockStoreCache, StoreSettings storeSettings, INodeLifetime nodeLifetime)
                : base(loggerFactory, walletManager, chain, network, blockStoreCache, storeSettings, nodeLifetime)
            {
            }

            public void SetWalletTip(ChainedBlock tip)
            {
                base.walletTip = tip;
            }
        }
    }
}
