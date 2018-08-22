using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests.Notifications
{
    public class BlockObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            var observer = new BlockObserver(walletSyncManager.Object);
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);
            
            observer.OnNext(chainedHeaderBlock);

            walletSyncManager.Verify(w => w.ProcessBlock(block), Times.Exactly(1));
        }
    }
}