using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// The CrossChainTransactionMonitor examines transactions to detect deposit and withdrawal transactions.
    /// The transactions we are interested move funds into our multi-sig and also have an additional output with
    /// an OP_RETURN and the destination address on the counter chain.
    /// When an appropriate transaction is detected we create a session in our session manager and continue monitoring
    /// our chain.
    /// The monitor also detects the counter transaction and reads a hash that allows us to link the
    /// monitor transaction with the counter transaction to verify the integrity of the two chains.
    /// </summary>
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
