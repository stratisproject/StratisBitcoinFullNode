using System.Threading.Tasks;
using NBitcoin;
using Moq;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;
using System.Collections.ObjectModel;

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
        public void Initialize_BlockOnChain_DoesNotChangeWalletManager()
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
        public void Initialize_BlockNotChain_RecoversWalletManagerUsingWallet()
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

            // verify the walletmanager is recovered using the fork block and it's tip is set to it.
            this.walletManager.Verify(w => w.RemoveBlocks(It.Is<ChainedBlock>(c => c.Header.GetHash() == forkBlockHash)), Times.Exactly(1));
            this.walletManager.VerifySet(w => w.WalletTipHash = forkBlockHash);
            Assert.Equal(walletSyncManager.WalletTip.HashBlock.ToString(), forkBlock.HashBlock.ToString());
            Assert.True(task.IsCompleted);
        }
    }
}
