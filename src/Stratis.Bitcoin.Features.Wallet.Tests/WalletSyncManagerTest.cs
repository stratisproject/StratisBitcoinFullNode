using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletSyncManagerTest : LogsTestBase
    {
        public abstract class MockWalletManager : WalletManager
        {
            public MockWalletManager(Network network, ChainIndexer chainIndexer) : base(new Mock<ILoggerFactory>().Object,
                    network, chainIndexer, new WalletSettings(NodeSettings.Default(network)),
                    NodeSettings.Default(network).DataFolder, new Mock<IWalletFeePolicy>().Object,
                    new Mock<IAsyncProvider>().Object, new Mock<INodeLifetime>().Object, DateTimeProvider.Default,
                    new Mock<IScriptAddressReader>().Object, new Mock<IWalletRepository>().Object)
            {
            }
        }

        private ChainIndexer chainIndexer;
        private Mock<MockWalletManager> walletManager;
        private Mock<IBlockStore> blockStore;
        private Mock<INodeLifetime> nodeLifetime;
        private Mock<IWalletRepository> walletRepository;
        private WalletSyncManager walletSyncManager;
        private ILoggerFactory loggerFactory;
        private StoreSettings storeSettings;
        private ISignals signals;
        private IAsyncProvider asyncProvider;
        private string walletName;

        private ChainedHeader walletTip;

        public WalletSyncManagerTest()
        {
            this.SetupMockObjects(new ChainIndexer(KnownNetworks.StratisMain));
        }

        private void SetupMockObjects(ChainIndexer chainIndexer, List<Block> blocks = null)
        {
            this.chainIndexer = chainIndexer;
            this.storeSettings = new StoreSettings(new NodeSettings(KnownNetworks.StratisMain));
            this.walletManager = new Mock<MockWalletManager>(this.Network, this.chainIndexer) { CallBase = true };
            this.blockStore = new Mock<IBlockStore>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.walletRepository = Mock.Get(((WalletManager)this.walletManager.Object).WalletRepository);
            this.loggerFactory = new LoggerFactory();
            this.signals = new Signals.Signals(new LoggerFactory(), null);
            this.asyncProvider = new AsyncProvider(new LoggerFactory(), this.signals, this.nodeLifetime.Object);
            this.walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, this.Network,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider, this.nodeLifetime.Object);
            this.walletName = "test";
            this.walletTip = this.chainIndexer.Tip;

            this.walletRepository.Setup(w => w.GetWalletNames()).Returns(() => {
                return (this.walletName == null) ? new List<string> { } : new List<string> { this.walletName };
            });

            // Mock wallet repository's 'RewindWallet'.
            this.walletRepository.Setup(r => r.RewindWallet(It.IsAny<string>(), It.IsAny<ChainedHeader>())).Returns((string name, ChainedHeader chainedHeader) => {
                this.walletTip = (chainedHeader == null) ? null : this.walletTip.FindFork(chainedHeader);
                return (true, new List<(uint256, DateTimeOffset)>());
            });

            // Mock wallet repository's 'FindFork'.
            this.walletRepository.Setup(r => r.FindFork(this.walletName, It.IsAny<ChainedHeader>())).Returns((string name, ChainedHeader chainedHeader) =>
            {
                return (this.walletTip == null) ? null : chainedHeader.FindFork(this.walletTip);
            });

            if (blocks != null)
            {
                // Setup blockstore to return blocks on the chain.
                var blockDict = blocks.ToDictionary(b => b.GetHash(), b => b);
                this.blockStore.Setup(b => b.GetBlocks(It.IsAny<List<uint256>>()))
                    .Returns((List<uint256> blockHashes) => blockHashes.Select(h => blockDict[h]).ToList());
            }
        }

        [Fact]
        public void Start_HavingPrunedStoreSetting_ThrowsWalletException()
        {
            this.storeSettings.AmountOfBlocksToKeep = 1;
            this.storeSettings.PruningEnabled = true;

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider, new Mock<INodeLifetime>().Object);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.Start();
            });
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        // NOTE: The test attempts to set the wallet tip height to a height that has not been processed by the wallet.
        /*
        [Fact]
        public void Start_BlockOnChain_DoesNotReorgWalletManager()
        {
            this.storeSettings.AmountOfBlocksToKeep = 0;

            this.SetupMockObjects(WalletTestsHelpers.PrepareChainWithBlock());

            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chainIndexer.Tip.Header.GetHash());

            this.walletSyncManager.OrchestrateWalletSync();

            //this.walletManager.Verify(w => w.GetFirstWalletBlockLocator(), Times.Exactly(0));
            this.walletManager.Verify(w => w.RemoveBlocks(It.IsAny<ChainedHeader>()), Times.Exactly(0));
        }
        */

        // TODO: Investigate the relevance of this test and remove it or fix it.
        // NOTE: The test attempts to set the wallet tp to a height that has not been processed by the wallet.
        /*
        [Fact]
        public void Start_BlockNotChain_ReorgsWalletManagerUsingWallet()
        {
            this.storeSettings.AmountOfBlocksToKeep = 0;
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(5, KnownNetworks.StratisMain);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(125)); // try to load non-existing block to get chain to return null.

            ChainedHeader forkBlock = this.chainIndexer.GetHeader(3); // use a block as the fork to recover to.
            uint256 forkBlockHash = forkBlock.Header.GetHash();
            //this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
               // .Returns(new Collection<uint256> { forkBlockHash });

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider, new Mock<INodeLifetime>().Object);

            walletSyncManager.Start();

            // verify the walletmanager is reorged using the fork block and it's tip is set to it.
            this.walletManager.Verify(w => w.RemoveBlocks(It.Is<ChainedHeader>(c => c.Header.GetHash() == forkBlockHash)));
            //this.walletManager.VerifySet(w => w.WalletTipHash = forkBlockHash);
            Assert.Equal(walletSyncManager.WalletTip.HashBlock.ToString(), forkBlock.HashBlock.ToString());
        }
        */

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is the same as the <see cref="WalletSyncManager.WalletTip"/> pass it directly to the <see cref="WalletManager"/>
        /// and set it as the new WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashSameAsWalletTip_PassesBlockToManagerWithoutReorg()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.SetupMockObjects(result.Chain, result.Blocks);

            walletSyncManager.OrchestrateWalletSync();

            this.walletRepository.Verify(r => r.RewindWallet(It.IsAny<string>(), It.IsAny<ChainedHeader>()), Times.Never());
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is not on the best chain
        /// look for the point at which the chain forked and remove blocks after that fork point from the <see cref="WalletManager"/>.
        /// After removing those blocks use the <see cref="BlockStore"/> to retrieve blocks on the best chain and use those to catchup the WalletManager.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockNotOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer LeftChain, ChainIndexer RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks) result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, KnownNetworks.StratisMain, 2);

            // Left side chain containing the 'old' fork.
            ChainIndexer leftChainIndexer = result.LeftChain;

            // Right side chain containing the 'new' fork. Work on this.
            this.SetupMockObjects(result.RightChain);

            // Setup blockstore to return blocks on the chain.
            var blockDict = result.LeftForkBlocks.Concat(result.RightForkBlocks).Distinct().ToDictionary(b => b.GetHash(), b => b);
            this.blockStore.Setup(b => b.GetBlocks(It.IsAny<List<uint256>>()))
                .Returns((List<uint256> blockHashes) => blockHashes.Select(h => blockDict[h]).ToList());

            // Set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            this.walletManager.Object.RewindWallet(this.walletName, leftChainIndexer.GetHeader(result.LeftForkBlocks[3].Header.GetHash()));

            // Rewind identifies height 2 as the last common block instead.
            Assert.Equal(2, this.walletTip.Height);

            // Sync will start at block 3 and end at block 5 which is the last block on the "right" chain.
            this.walletSyncManager.OrchestrateWalletSync();

            this.walletRepository.Verify(r => r.ProcessBlocks(
                It.Is<IEnumerable<(ChainedHeader header, Block block)>>(c => string.Join(",", c.Select(b => b.header.Height)) == "3,4,5"),
                It.IsAny<string>()));
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        /*
        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is on the best chain
        /// see which blocks are missing and retrieve blocks from the <see cref="BlockStore"/> to catchup the <see cref="WalletManager"/>.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock__BlockOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.SetupMockObjects(result.Chain, result.Blocks);

            // Set 2nd block as tip.
            //this.walletSyncManager.SyncFromHeight(3, this.walletName);

            // Process 4th block in the list does not have same prevhash as which is loaded
            Block blockToProcess = result.Blocks[3];
            blockToProcess.SetPrivatePropertyValue("BlockSize", 1L);

            this.walletSyncManager.ProcessBlock(blockToProcess);

            //verify manager processes each missing block until caught up.
            this.walletRepository.Verify(r => r.ProcessBlocks(
                It.Is<IEnumerable<(ChainedHeader header, Block block)>>(c => string.Join(",", c.Select(b => b.header.Height)) == "3,4,5"),
                It.IsAny<string>()));
        }

        /// <summary>
        /// When using the <see cref="BlockStore"/> to catchup on the <see cref="WalletManager"/> and the <see cref="Block"/> is not in the BlockStore yet try to wait until it arrives.
        /// If it does use it to catchup the WalletManager.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockArrivesLateInBlockStoreCache_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.SetupMockObjects(result.Chain, result.Blocks);

            // Rewind the wallet.
            this.walletSyncManager.SyncFromHeight(3, this.walletName);

            // Sync to catch up.
            this.walletSyncManager.OrchestrateWalletSync();

            // Expect that this will sync blocks 3, 4 and 5.
            this.walletRepository.Verify(r => r.ProcessBlocks(
                It.Is<IEnumerable<(ChainedHeader header, Block block)>>(c => string.Join(",", c.Select(b => b.header.Height)) == "3,4,5"),
                It.IsAny<string>()));
        }
        */
        [Fact]
        public void ProcessTransaction_CallsWalletManager()
        {
            this.SetupMockObjects(WalletTestsHelpers.GenerateChainWithHeight(5, KnownNetworks.StratisMain));

            var transaction = new Transaction
            {
                Version = 15
            };

            this.walletSyncManager.ProcessTransaction(transaction);

            this.walletRepository.Verify(w => w.ProcessTransaction(this.walletName, transaction, null));
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the closest <see cref="Block"/> to the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateMatchingBlocksOnChain_UpdatesUsingClosestBlock()
        {
            this.SetupMockObjects(WalletTestsHelpers.GenerateChainWithHeight(5, KnownNetworks.StratisMain));

            this.walletSyncManager.SyncFromDate(this.chainIndexer.GetHeader(3).Header.BlockTime.DateTime.AddSeconds(1));

            uint256 expectedHash = this.chainIndexer.GetHeader(3).HashBlock;
            Assert.Equal(this.walletTip.HashBlock, expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the first <see cref="Block"/> if there is no block near the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateNotMatchingBlocksOnChain_UpdatesUsingFirstBlock()
        {
            this.SetupMockObjects(WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain));

            this.walletSyncManager.SyncFromDate(new DateTime(1900, 1, 1)); // date before any block.

            uint256 expectedHash = this.chainIndexer.GetHeader(0).HashBlock;
            Assert.Equal(this.walletTip.HashBlock, expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the genesis <see cref="Block"/> if there is no block on the chain.
        /// </summary>
        [Fact]
        public void SyncFromDate_EmptyChain_UpdateUsingGenesisBlock()
        {
            this.chainIndexer = new ChainIndexer(KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider, new Mock<INodeLifetime>().Object);

            walletSyncManager.SyncFromDate(new DateTime(1900, 1, 1)); // date before any block.

            Assert.Null(walletSyncManager.WalletTip);
        }

        [Fact]
        public void SyncFromHeight_BlockWithHeightOnChain_UpdatesWalletTipOnWalletAndWalletSyncManagers()
        {
            this.SetupMockObjects(WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain));

            this.walletSyncManager.SyncFromHeight(2);

            uint256 expectedHash = this.chainIndexer.GetHeader(1).HashBlock;
            Assert.Equal(this.walletTip.HashBlock, expectedHash);
        }

        [Fact]
        public void SyncFromHeight_NoBlockWithGivenHeightOnChain_ThrowsWalletException()
        {
            this.SetupMockObjects(WalletTestsHelpers.GenerateChainWithHeight(1, KnownNetworks.StratisMain));

            Assert.Throws<WalletException>(() =>
            {
                this.walletSyncManager.SyncFromHeight(2, this.walletName);
            });
        }

        /// <summary>
        /// Don't enqueue new <see cref="Block"/>s - to be processed by <see cref="WalletSyncManager"/> - when there is no Wallet.
        /// </summary>c
        [Fact]
        public void ProcessBlock_With_No_Wallet_Processing_Is_Ignored()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(1, KnownNetworks.StratisMain);

            this.SetupMockObjects(result.Chain, result.Blocks);

            // No wallets.
            this.walletName = null;

            this.walletSyncManager.OrchestrateWalletSync();

            // Expect that no blocks will be synced.
            this.walletRepository.Verify(r => r.ProcessBlocks(
                It.IsAny<IEnumerable<(ChainedHeader header, Block block)>>(),
                It.IsAny<string>()), Times.Never);
        }

        private static ChainedHeader ExpectChainedBlock(ChainedHeader block)
        {
            return It.Is<ChainedHeader>(c => c.Header.GetHash() == block.Header.GetHash());
        }

        private static Block ExpectBlock(Block block)
        {
            return It.Is<Block>(b => b.GetHash() == block.GetHash());
        }

        private static void WaitLoop(Func<bool> act, string failureReason, int millisecondsTimeout = 50)
        {
            if (failureReason == null)
                throw new ArgumentNullException(nameof(failureReason));

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

        private uint256 AssertTipBlockHash(IWalletSyncManager walletSyncManager, int blockHeight)
        {
            uint256 expectedBlockHash = this.chainIndexer.GetHeader(blockHeight).Header.GetHash();

            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(),
                $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            return expectedBlockHash;
        }
    }
}
