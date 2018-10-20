//using Stratis.Bitcoin.Signals;

//namespace City.Chain.Features.SimpleWallet.Notifications
//{
//    /// <summary>
//    /// Observer that receives notifications about the arrival of new <see cref="Transaction"/>s.
//    /// </summary>
//    public class TransactionObserver : SignalObserver<NBitcoin.Transaction>
//    {
//        private readonly SimpleWalletService walletService;

//        public TransactionObserver(SimpleWalletService walletService)
//        {
//            this.walletService = walletService;
//        }

//        /// <summary>
//        /// Manages what happens when a new transaction is received.
//        /// </summary>
//        /// <param name="transaction">The new transaction</param>
//        protected override void OnNextCore(NBitcoin.Transaction transaction)
//        {
//            this.walletService.ProcessTransaction(transaction);
//        }
//    }
//}
