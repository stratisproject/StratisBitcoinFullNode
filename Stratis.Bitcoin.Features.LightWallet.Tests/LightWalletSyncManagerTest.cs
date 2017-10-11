using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Tests;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using System;
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

        [Fact]
        public void Start_WalletTipOnChain_SetsWalletTip_HavingEarliestWalletHeight_StartsSyncFromEarliestHeight()
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

        [Fact]
        public void Start_WalletTipOnChain_SetsWalletTip_NotHavingEarliestWalletHeight_StartsSyncFromOldestWalletCreationTime()
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