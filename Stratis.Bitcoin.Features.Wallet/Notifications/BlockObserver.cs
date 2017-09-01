using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.Wallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
	public class BlockObserver : SignalObserver<Block>
    {
        private readonly IWalletSyncManager walletSyncManager;

        public BlockObserver(IWalletSyncManager walletSyncManager)
        {
            this.walletSyncManager = walletSyncManager;
        }

        /// <summary>
        /// Manages what happens when a new block is received.
        /// </summary>
        /// <param name="block">The new block</param>
        protected override void OnNextCore(Block block)
        {            
            this.walletSyncManager.ProcessBlock(block);
        }
    }
}
