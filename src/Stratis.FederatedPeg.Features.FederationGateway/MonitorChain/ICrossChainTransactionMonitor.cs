using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public interface ICrossChainTransactionMonitor : IDisposable
    {
        /// <summary>
        /// Sets up the monitor for the appropriate chain.
        /// Sets the multisig redeem script that we are interested in. (There are two possibilities mainchain or sidechain address.)
        /// Initilize and load the store.
        /// </summary>
        void Initialize();

        /// <summary>
        /// The BlockObserver will tell us when a block is incoming. Here we look at each block and iterate through the transactions
        /// while passing the work onto the ProcessTransaction method.
        /// </summary>
        /// <param name="block">The block received from the BlockObserver.</param>
        void ProcessBlock(Block block);

        /// <summary>
        /// Does the monitoring work by identifying whether our transaction has the relevant script then
        /// finding an op_return (if present). These two attributes of the transaction identify a relevant
        /// transaction that we are interested in processing further.
        /// </summary>
        /// <param name="transaction">The transaction that we are examining.</param>
        /// <param name="block">The block that contains the transaction.</param>
        /// <param name="blockNumber">The block number.</param>
        void ProcessTransaction(Transaction transaction, Block block, int blockNumber);

        /// <summary>
        /// This method creates a session that will handle the co-ordination of cross chain transaction and the signing of
        /// the multi-sig transactions by the federation.
        /// </summary>
        /// <param name="crossChainTransactionInfo"></param>
        void CreateSession(CrossChainTransactionInfo crossChainTransactionInfo);
    }
}
