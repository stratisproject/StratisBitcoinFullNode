using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests.Notifications
{
    public class BlockObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            BlockObserver observer = new BlockObserver(walletSyncManager.Object);
            Block block = new Block();

            observer.OnNext(block);

            walletSyncManager.Verify(w => w.ProcessBlock(block), Times.Exactly(1));
        }
    }
}
