using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
    public class BlockObserver : SignalObserver<Block>
    {
        private readonly IGeneralPurposeWalletSyncManager walletSyncManager;

        public BlockObserver(IGeneralPurposeWalletSyncManager walletSyncManager)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));

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
