using NBitcoin;
using Stratis.Bitcoin;

namespace Stratis.Bitcoin.Wallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
    /// </summary>
	public class BlockObserver : SignalObserver<Block>
    {
        private readonly ConcurrentChain chain;
        private readonly IWalletManager walletManager;

        public BlockObserver(ConcurrentChain chain, IWalletManager walletManager)
        {
            this.chain = chain;
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Manages what happens when a new block is received.
        /// </summary>
        /// <param name="block">The new block</param>
        protected override void OnNextCore(Block block)
        {            
            var hash = block.Header.GetHash();
            var height = this.chain.GetBlock(hash).Height;

            this.walletManager.ProcessBlock(height, block);
        }
    }
}
