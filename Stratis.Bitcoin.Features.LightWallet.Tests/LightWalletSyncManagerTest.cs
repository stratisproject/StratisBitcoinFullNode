using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Tests;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using Xunit;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using System.Threading;

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

            this.walletManager = new Mock<IWalletManager>();
            this.chain = new ConcurrentChain();
            this.blockNotification = new Mock<IBlockNotification>();
            this.signals = new Mock<ISignals>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.asyncLoopFactory = new Mock<IAsyncLoopFactory>();
            this.network = Network.StratisMain;
        }

        [Fact]
        public void Start_StartsBlockAndTransactionObserver()
        {
            var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

            lightWalletSyncManager.Start();

            this.signals.Verify(s => s.SubscribeForBlocks(It.IsAny<IObserver<Block>>()), Times.Exactly(1));
            this.signals.Verify(s => s.SubscribeForTransactions(It.IsAny<IObserver<Transaction>>()), Times.Exactly(1));
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> does not contain a single <see cref="Wallet.Wallet"/> the <see cref="LightWalletSyncManager.WalletTip"/>
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
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can be found 
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
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can be found 
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
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found 
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
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedBlock>(c => c.HashBlock == this.chain.GetBlock(2).HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            var expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found 
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
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedBlock>(c => c.HashBlock == this.chain.Genesis.HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            var expectedBlockHash = this.chain.Genesis.HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found 
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
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedBlock>(c => c.HashBlock == this.chain.GetBlock(3).HashBlock)));

            // verify that the sync is started using the height from GetEarliestWalletHeight
            var expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="WalletManager.WalletTipHash"/> can not be found 
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
            this.walletManager.Verify(v => v.RemoveBlocks(It.Is<ChainedBlock>(c => c.HashBlock == this.chain.Genesis.HashBlock)));

            // verify that the sync is started using the first block.
            var expectedBlockHash = this.chain.GetBlock(1).HashBlock;
            this.blockNotification.Verify(b => b.SyncFrom(expectedBlockHash));
            Assert.Equal(lightWalletSyncManager.WalletTip.HashBlock, expectedBlockHash);
            this.walletManager.VerifySet(b => b.WalletTipHash = expectedBlockHash);
        }

        /// <summary>
        /// If the <see cref="WalletManager"/> contains a <see cref="Wallet.Wallet"/> and the <see cref="ConcurrentChain"/> does not contain any blocks
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
            var expectedBlockHash = this.chain.Genesis.HashBlock;
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
            this.signals.Setup(s => s.SubscribeForBlocks(It.IsAny<BlockObserver>()))
                .Returns(blockSub.Object);
            this.signals.Setup(s => s.SubscribeForTransactions(It.IsAny<TransactionObserver>()))
                .Returns(transSub.Object);
            this.walletManager.SetupGet(w => w.ContainsWallets)
                .Returns(false);

            var asyncLoop = new Mock<IAsyncLoop>();
            this.asyncLoopFactory.Setup(
                a => a.RunUntil(
                    "WalletFeature.DownloadChain",
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
        public void SyncFromHeight_TipLessChain_ThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                this.chain = new ConcurrentChain();
                var lightWalletSyncManager = new LightWalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chain, this.network,
                    this.blockNotification.Object, this.signals.Object, this.nodeLifetime.Object, this.asyncLoopFactory.Object);

                lightWalletSyncManager.SyncFromHeight(3);
            });
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
                    "WalletFeature.DownloadChain",
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

            var expectedBlockHash = this.chain.GetBlock(1).HashBlock;
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
                    "WalletFeature.DownloadChain",
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
                               "WalletFeature.DownloadChain",
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

            var expectedBlockHash = this.chain.GetBlock(1).HashBlock;
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
                    "WalletFeature.DownloadChain",
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

            this.walletManager.Verify(w => w.ProcessTransaction(transaction, null, null));
        }
    }
}