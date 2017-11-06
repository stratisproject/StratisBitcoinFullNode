using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
    public class BlockObserver : SignalObserver<Block>
    {
        private readonly IWatchOnlyWalletManager walletManager;

        public BlockObserver(IWatchOnlyWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Manages what happens when a new block is received.
        /// </summary>
        /// <param name="block">The new block.</param>
        protected override void OnNextCore(Block block)
        {
            this.walletManager.ProcessBlock(block);
        }
    }
}
