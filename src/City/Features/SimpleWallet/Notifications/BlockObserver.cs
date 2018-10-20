//using NBitcoin;
//using Stratis.Bitcoin.Signals;

//namespace City.Chain.Features.SimpleWallet.Notifications
//{
//    /// <summary>
//    /// Observer that receives notifications about the arrival of new <see cref="Block"/>s.
//    /// </summary>
//    public class BlockObserver : SignalObserver<Block>
//    {
//        private readonly SimpleWalletService walletService;

//        public BlockObserver(SimpleWalletService walletService)
//        {
//            this.walletService = walletService;
//        }

//        /// <summary>
//        /// Manages what happens when a new block is received.
//        /// </summary>
//        /// <param name="block">The new block.</param>
//        protected override void OnNextCore(Block block)
//        {
//            this.walletService.ProcessBlock(block);
//        }
//    }
//}
