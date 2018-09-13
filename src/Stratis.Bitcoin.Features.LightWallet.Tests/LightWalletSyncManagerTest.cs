using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.LightWallet.Tests
{
    public class LightWalletSyncManagerTest : LogsTestBase
    {
        private Mock<IWalletManager> walletManager;
        private ConcurrentChain chain;
        private Mock<IBlockNotification> blockNotification;
        private Mock<ISignals> signals;
        private Mock<INodeLifetime> nodeLifetime;
        private Mock<IAsyncLoopFactory> asyncLoopFactory;
        private Network network;

        public LightWalletSyncManagerTest()
        {
            this.network = KnownNetworks.StratisMain;

            this.walletManager = new Mock<IWalletManager>();
            this.chain = new ConcurrentChain(this.network);
            this.blockNotification = new Mock<IBlockNotification>();
            this.signals = new Mock<ISignals>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
        }

        [Fact]
        public void Start_StartsBlockAndTransactionObserver()
        {
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            this.signals.Verify(s => s.SubscribeForBlocksConnected(It.IsAny<IObserver<ChainedHeaderBlock>>()), Times.Exactly(1));
            this.signals.Verify(s => s.SubscribeForTransactions(It.IsAny<IObserver<Transaction>>()), Times.Exactly(1));
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> does not contain a single <see cref="Wallet"/> the <see cref="LightWalletSyncManager.WalletTip"/>
        /// must be set to the <see cref="ConcurrentChain.Tip"/>.
        /// </summary>
        [Fact]
        public void Start_WithoutWalletsLoadedOnWalletManager_SetsWalletTipToChainTip()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(false);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, this.chain.Tip.HashBlock);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can be found
        /// on the <see cref="ConcurrentChain"/> request the earliest wallet block height from the WalletManager.
        /// Start syncing from that height and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipOnChain_HavingEarliestWalletHeight_StartsSyncFromEarliestHeight()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chain.GetBlock(2).HashBlock);
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns(1);

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, this.chain.GetBlock(1).HashBlock);
            this.blockNotification.Verify(b => b.SyncFrom(this.chain.GetBlock(1).HashBlock));
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can be found
        /// on the <see cref="ConcurrentChain"/> request the earliest wallet block height from the WalletManager.
        /// Start syncing from that height and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipOnChain_NotHavingEarliestWalletHeight_StartsSyncFromOldestWalletCreationTime()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chain.GetBlock(2).HashBlock);
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns((int?)null);
            this.walletManager.Setup(w => w.GetOldestWalletCreationTime())
                .Returns(new DateTimeOffset(new DateTime(2017, 1, 2)));

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, this.chain.GetBlock(2).HashBlock);
            this.blockNotification.Verify(b => b.SyncFrom(this.chain.GetBlock(2).HashBlock));
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found
        /// on the <see cref="ConcurrentChain"/> we are not on the best chain anymore.
        /// Lookup the point at which the chain forked and remove all blocks after that point.
        /// Start syncing from the earliest wallet height if that is before the fork point and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipNotOnChain_ReorgHavingEarliestWalletHeight_StartsSyncFromEarliestHeight()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(15));
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns(1);
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new List<uint256>() { this.chain.GetBlock(2).HashBlock });

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            // verify that the walletmanager removes blocks using the block locator.
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedHeader>(c => c.HashBlock == this.chain.GetBlock(2).HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            uint256 expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found
        /// on the <see cref="ConcurrentChain"/> we are not on the best chain anymore.
        /// Lookup the point at which the chain forked and remove all blocks after that point.
        /// Start syncing from the fork point if that is after the fork point and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipNotOnChain_ReorgHavingEarliestWalletHeight_StartsSyncFromForkPoint()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(15));
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns(1);
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new List<uint256>() { this.chain.Genesis.HashBlock });

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            // verify that the walletmanager removes blocks using the block locator.
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedHeader>(c => c.HashBlock == this.chain.Genesis.HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            uint256 expectedBlockHash = this.chain.Genesis.HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found
        /// on the <see cref="ConcurrentChain"/> we are not on the best chain anymore.
        /// Lookup the point at which the chain forked and remove all blocks after that point.
        /// Start syncing from the oldest wallet creation time if the WalletManager does not have an earliest wallet height.
        /// If the blocktime of the fork block is after the earliest wallet creation time sync using the earliest wallet creation time
        /// and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipNotOnChain_ReorgNotHavingEarliestWalletHeight_StartsSyncFromOldestWalletCreationTime()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(5, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(15));
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns((int?)null);
            this.walletManager.Setup(w => w.GetOldestWalletCreationTime())
                .Returns(new DateTimeOffset(new DateTime(2017, 1, 1)));
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new List<uint256>() { this.chain.GetBlock(3).HashBlock });

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            // verify that the walletmanager removes blocks using the block locator.
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedHeader>(c => c.HashBlock == this.chain.GetBlock(3).HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            uint256 expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found
        /// on the <see cref="ConcurrentChain"/> we are not on the best chain anymore.
        /// Lookup the point at which the chain forked and remove all blocks after that point.
        /// Start syncing from the oldest wallet creation time if the WalletManager does not have an earliest wallet height.
        /// If the blocktime of the fork block is before the earliest wallet creation time sync using fork block blocktime
        /// and set the <see cref="LightWalletSyncManager.WalletTip"/> to that block.
        /// </summary>
        [Fact]
        public void Start_WalletTipNotOnChain_ReorgNotHavingEarliestWalletHeight_StartsSyncFromForkBlockTime()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(15));
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns((int?)null);
            this.walletManager.Setup(w => w.GetOldestWalletCreationTime())
                .Returns(new DateTimeOffset(new DateTime(2017, 2, 1)));
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new List<uint256>() { this.chain.Genesis.HashBlock });

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            // verify that the walletmanager removes blocks using the block locator.
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedHeader>(c => c.HashBlock == this.chain.Genesis.HashBlock)));

            // verify that the sync is started using the first block.
            uint256 expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet"/> and the <see cref="ConcurrentChain"/> does not contain any blocks
        /// start the sync from the genesis block and set the <see cref="LightWalletSyncManager.WalletTip"/> to that.
        /// </summary>
        [Fact]
        public void Start_HavingWalletsAndEmptyChain_StartsSyncFromGenesisBlock()
        {
            this.chain = new ConcurrentChain(this.network);
            this.walletManager.Setup(w => w.ContainsWallets)
                .Returns(true);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(this.chain.Genesis.HashBlock);
            this.walletManager.Setup(w => w.GetEarliestWalletHeight())
                .Returns((int?)null);
            this.walletManager.Setup(w => w.GetOldestWalletCreationTime())
                .Returns(new DateTimeOffset(new DateTime(2000, 1, 1)));

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            // verify that the sync is started using genesis block
            uint256 expectedBlockHash = this.chain.Genesis.HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        [Fact]
        public void Stop_DisposesDependencies()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(1, this.network);
            var blockSub = new Mock<IDisposable>();
            var transSub = new Mock<IDisposable>();
            this.signals.Setup(s => s.SubscribeForBlocksConnected(It.IsAny<BlockObserver>()))
                .Returns(blockSub.Object);
            this.signals.Setup(s => s.SubscribeForTransactions(It.IsAny<TransactionObserver>()))
                .Returns(transSub.Object);
            this.walletManager.SetupGet(w => w.ContainsWallets)
                .Returns(false);

            var asyncLoop = new Mock<IAsyncLoop>();
            this.asyncLoopFactory.Setup(
                a => a.RunUntil(
                    "LightWalletSyncManager.SyncFromHeight",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Func<bool>>(),
                    It.IsAny<Action>(),
                    It.IsAny<Action<Exception>>(),
                    TimeSpans.FiveSeconds))
                .Returns(asyncLoop.Object);

            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();
            lightWalletSyncManager.SyncFromHeight(3);
            lightWalletSyncManager.Stop();

            asyncLoop.Verify(b => b.Dispose());
            blockSub.Verify(b => b.Dispose());
            transSub.Verify(b => b.Dispose());
        }

        [Fact]
        public void SyncFromHeight_EmptyChain_StartsAsyncLoopToCatchup()
        {
            this.chain = new ConcurrentChain(this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromHeight(3);

            this.asyncLoopFactory.Verify(
                a => a.RunUntil(
                    "LightWalletSyncManager.SyncFromHeight",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Func<bool>>(),
                    It.IsAny<Action>(),
                    It.IsAny<Action<Exception>>(),
                    TimeSpans.FiveSeconds));
            this.nodeLifetime.VerifyGet(n => n.ApplicationStopping);
        }

        [Fact]
        public void SyncFromHeight_NegativeHeight_ThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                this.chain = new ConcurrentChain(this.network);
                var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                    this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

                lightWalletSyncManager.SyncFromHeight(-1);
            });
        }

        [Fact]
        public void SyncFromHeight_ChainTipAfterGivenHeight_StartsSyncFromGivenHeight()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(2, this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromHeight(1);

            uint256 expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        [Fact]
        public void SyncFromHeight_ChainTipBeforeGivenHeight_StartsAsyncLoopToCatchupChain()
        {
            var asyncLoop = new Mock<IAsyncLoop>();

            this.chain = WalletTestsHelpers.GenerateChainWithHeight(2, this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromHeight(3);

            this.asyncLoopFactory.Verify(
                a => a.RunUntil(
                    "LightWalletSyncManager.SyncFromHeight",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Func<bool>>(),
                    It.IsAny<Action>(),
                    It.IsAny<Action<Exception>>(),
                    TimeSpans.FiveSeconds));
            this.nodeLifetime.VerifyGet(n => n.ApplicationStopping);
        }

        [Fact]
        public void SyncFromDate_EmptyChain_StartsAsyncLoopToCatchup()
        {
            this.chain = new ConcurrentChain(this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromDate(new DateTime(2017, 1, 1));

            this.asyncLoopFactory.Verify(
                           a => a.RunUntil(
                               "LightWalletSyncManager.SyncFromDate",
                               It.IsAny<CancellationToken>(),
                               It.IsAny<Func<bool>>(),
                               It.IsAny<Action>(),
                               It.IsAny<Action<Exception>>(),
                               TimeSpans.FiveSeconds));
            this.nodeLifetime.VerifyGet(n => n.ApplicationStopping);
        }

        [Fact]
        public void SyncFromDate_ChainTipAfterGivenDate_StartsSyncFromHeightAtTime()
        {
            this.chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromDate(this.chain.GetBlock(1).Header.BlockTime.DateTime);

            uint256 expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        [Fact]
        public void SyncFromDate_ChainTipBeforeGivenDate_StartsAsyncLoopToCatchupChain()
        {
            var asyncLoop = new Mock<IAsyncLoop>();

            this.chain = WalletTestsHelpers.GenerateChainWithHeight(2, this.network);
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.SyncFromDate(this.chain.Tip.Header.BlockTime.DateTime.AddDays(15));

            this.asyncLoopFactory.Verify(
                a => a.RunUntil(
                    "LightWalletSyncManager.SyncFromDate",
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Func<bool>>(),
                    It.IsAny<Action>(),
                    It.IsAny<Action<Exception>>(),
                    TimeSpans.FiveSeconds));
            this.nodeLifetime.VerifyGet(n => n.ApplicationStopping);
        }

        [Fact]
        public void ProcessTransaction_CallsWalletManager()
        {
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            var transaction = new Transaction()
            {
                Version = 15
            };

            lightWalletSyncManager.ProcessTransaction(transaction);

            this.walletManager.Verify(w => w.ProcessTransaction(transaction, null, null, true));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is the same as the <see cref="LightWalletSyncManager.WalletTip"/> pass it directly to the <see cref="WalletManager"/>
        /// and set it as the new WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashSameAsWalletTip_PassesBlockToManagerWithoutReorg()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chain = result.Chain;
            List<Block> blocks = result.Blocks;
            var lightWalletSyncManager = new LightWalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);
            lightWalletSyncManager.SetWalletTip(this.chain.GetBlock(3));

            Block blockToProcess = blocks[3];
            lightWalletSyncManager.ProcessBlock(blockToProcess); //4th block in the list has same prevhash as which is loaded

            uint256 expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            Assert.Equal(expectedBlockHash, lightWalletSyncManager.WalletTip.Header.GetHash());
            this.walletManager.Verify(w => w.ProcessBlock(It.Is<Block>(b => b.GetHash() == blockToProcess.GetHash()), It.Is<ChainedHeader>(c => c.Header.GetHash() == expectedBlockHash)));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="LightWalletSyncManager.WalletTip"/> and is on the best chain
        /// see which blocks are missing notify the <see cref="BlockNotification"/> to sync from the wallet tip to catchup the <see cref="WalletManager"/>.
        /// Do not process the block or set it as a wallet tip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockOnBestChain_WalletTipBeforeNewTip_StartsSyncFromWalletTipWithoutProcessingBlock()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chain = result.Chain;
            List<Block> blocks = result.Blocks;
            var lightWalletSyncManager = new LightWalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
              this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            // set 2nd block as tip
            lightWalletSyncManager.SetWalletTip(this.chain.GetBlock(2));
            //process 4th block in the list does not have same prevhash as which is loaded
            Block blockToProcess = blocks[3];
            lightWalletSyncManager.ProcessBlock(blockToProcess);

            this.blockNotification.Verify(b => b.SyncFrom(this.chain.GetBlock(2).HashBlock));

            uint256 expectedBlockHash = this.chain.GetBlock(2).Header.GetHash();
            Assert.Equal(expectedBlockHash, lightWalletSyncManager.WalletTip.Header.GetHash());
            this.walletManager.Verify(w => w.ProcessBlock(It.IsAny<Block>(), It.IsAny<ChainedHeader>()), Times.Exactly(0));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="LightWalletSyncManager.WalletTip"/>
        /// and is on the best chain see if the wallettip is after the newtip.
        /// If this is the case use the old <see cref="LightWalletSyncManager.WalletTip"/> and  process the block using the <see cref="WalletManager"/>.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockOnBestChain_WalletTipAfterNewTip_StartsSyncFromNewTip()
        {
            (ConcurrentChain Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chain = result.Chain;
            List<Block> blocks = result.Blocks;
            var lightWalletSyncManager = new LightWalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
              this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            // set 2nd block as tip
            lightWalletSyncManager.SetWalletTip(this.chain.GetBlock(4));
            //process 4th block in the list does not have same prevhash as which is loaded
            Block blockToProcess = blocks[3];
            lightWalletSyncManager.ProcessBlock(blockToProcess);

            uint256 expectedBlockHash = this.chain.GetBlock(4).Header.GetHash();
            Assert.Equal(expectedBlockHash, lightWalletSyncManager.WalletTip.Header.GetHash());
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chain.GetBlock(4))));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="LightWalletSyncManager.WalletTip"/> and is not on the best chain
        /// look for the point at which the chain forked and remove blocks after that fork point from the <see cref="WalletManager"/>.
        /// After removing those blocks set the fork block as the <see cref="LightWalletSyncManager.WalletTip"/> and notify the <see cref="blockNotification"/> to start syncing from the fork.
        /// Do not process the block.
        /// </summary>
        [Fact]
        public void ProcessBlock_BlockNotOnBestChain_ReorgWalletTipBeforeNewTip_StartsSyncFromForkPointWithoutProcessingBlock()
        {
            (ConcurrentChain LeftChain, ConcurrentChain RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks) result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, KnownNetworks.StratisMain, 2);
            // left side chain containing the 'old' fork.
            ConcurrentChain leftChain = result.LeftChain;
            // right side chain containing the 'new' fork. Work on this.
            this.chain = result.RightChain;
            var lightWalletSyncManager = new LightWalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            // set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            lightWalletSyncManager.SetWalletTip(leftChain.GetBlock(result.LeftForkBlocks[3].Header.GetHash()));
            //process 5th block from the right side of the fork in the list does not have same prevhash as which is loaded.
            Block blockToProcess = result.RightForkBlocks[4];
            lightWalletSyncManager.ProcessBlock(blockToProcess);

            // walletmanager removes all blocks up to the fork.
            this.walletManager.Verify(w => w.RemoveBlocks(ExpectChainedBlock(this.chain.GetBlock(2))));

            //expect the wallet tip to be set to the fork and the sync to be started from that block.
            Assert.Equal(this.chain.GetBlock(2).HashBlock, lightWalletSyncManager.WalletTip.HashBlock);
            this.blockNotification.Verify(w => w.SyncFrom(this.chain.GetBlock(2).HashBlock));
            // expect no blocks to be processed.
            this.walletManager.Verify(w => w.ProcessBlock(It.IsAny<Block>(), It.IsAny<ChainedHeader>()), Times.Exactly(0));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="LightWalletSyncManager.WalletTip"/> and is not on the best chain
        /// look for the point at which the chain forked and remove blocks after that fork point from the <see cref="WalletManager"/>.
        /// After removing those blocks set the fork block as the <see cref="LightWalletSyncManager.WalletTip"/>
        /// Process the block if the new tip is before the <see cref="LightWalletSyncManager.WalletTip"/>.
        /// </summary>
        [Fact]
        public void ProcessBlock_BlockNotOnBestChain_ReorgWalletTipAfterNewTip_StartProcessingFromFork()
        {
            (ConcurrentChain LeftChain, ConcurrentChain RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks) result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, KnownNetworks.StratisMain, 2);
            // left side chain containing the 'old' fork.
            ConcurrentChain leftChain = result.LeftChain;
            // right side chain containing the 'new' fork. Work on this.
            this.chain = result.RightChain;

            var lightWalletSyncManager = new LightWalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
               this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            // set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            lightWalletSyncManager.SetWalletTip(leftChain.GetBlock(result.LeftForkBlocks[3].Header.GetHash()));
            //process 2nd block from the right side of the fork in the list does not have same prevhash as which is loaded.
            Block blockToProcess = result.RightForkBlocks[1];
            lightWalletSyncManager.ProcessBlock(blockToProcess);

            // walletmanager removes all blocks up to the fork.
            this.walletManager.Verify(w => w.RemoveBlocks(ExpectChainedBlock(this.chain.GetBlock(2))));

            //expect the wallet tip to be set to the fork and do not start the sync to be started from that block.
            Assert.Equal(this.chain.GetBlock(2).HashBlock, lightWalletSyncManager.WalletTip.HashBlock);
            this.blockNotification.Verify(w => w.SyncFrom(It.IsAny<uint256>()), Times.Exactly(0));
            // expect the block to be processed.
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[1]), ExpectChainedBlock(this.chain.GetBlock(2))));
        }

        private static ChainedHeader ExpectChainedBlock(ChainedHeader block)
        {
            return It.Is<ChainedHeader>(c => c.Header.GetHash() == block.Header.GetHash());
        }

        private static Block ExpectBlock(Block block)
        {
            return It.Is<Block>(b => b.GetHash() == block.GetHash());
        }

        private class LightWalletSyncManagerOverride : LightWalletSyncManager
        {
            public LightWalletSyncManagerOverride(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
                Network network, IBlockNotification blockNotification, ISignals signals, INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
                : base(loggerFactory, walletManager, chain, network, blockNotification, signals, nodeLifetime, asyncLoopFactory)
            {
            }

            public void SetWalletTip(ChainedHeader tip)
            {
                this.walletTip = tip;
            }
        }
    }
}
