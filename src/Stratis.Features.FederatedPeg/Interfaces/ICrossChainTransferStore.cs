using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>Interface for interacting with the cross-chain transfer database.</summary>
    public interface ICrossChainTransferStore : IDisposable
    {
        /// <summary>Initializes the cross-chain-transfer store.</summary>
        void Initialize();

        /// <summary>Starts the cross-chain-transfer store.</summary>
        void Start();

        /// <summary>
        /// For optimization purposes the current tip is not saved after receiving an empty block.
        /// This needs to be called to ensure the tip is persisted.
        /// </summary>
        Task SaveCurrentTipAsync();

        /// <summary>
        /// Records the mature deposits from <see cref="NextMatureDepositHeight"/> on the counter-chain.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// </summary>
        /// <param name="blockDeposits">The deposits in order of occurrence on the source chain.</param>
        /// <returns><c>True</c> if there may be more blocks and <c>false</c> otherwise.</returns>
        /// <remarks>
        /// The transfers are set to <see cref="CrossChainTransfer.Status"/> of <see cref="CrossChainTransferStatus.Partial"/>
        /// or <see cref="CrossChainTransferStatus.Rejected"/> depending on whether enough funds are available in the federation wallet.
        /// New partial transactions are recorded in the wallet to ensure that future transactions will not
        /// attempt to re-use UTXO's.
        /// </remarks>
        Task<bool> RecordLatestMatureDepositsAsync(IList<MaturedBlockDepositsModel> blockDeposits);

        /// <summary>Returns transactions by status. Orders the results by UTXO selection order.</summary>
        /// <param name="status">The status to get the transactions for.</param>
        /// <param name="sort">Set to <c>true</c> to sort the transfers by their earliest inputs.</param>
        /// <returns>An array of transactions.</returns>
        Task<Dictionary<uint256, Transaction>> GetTransactionsByStatusAsync(CrossChainTransferStatus status, bool sort = false);

        /// <summary>
        /// Updates partial transactions in the store with signatures obtained from the passed transactions.
        /// The <see cref="CrossChainTransferStatus.FullySigned"/> status is set on fully signed transactions.
        /// </summary>
        /// <param name="depositId">The deposit transaction to update.</param>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        /// <remarks>
        /// Changes to the transaction id caused by this operation will also be synchronised with the partial
        /// transaction that has been recorded in the wallet.
        /// </remarks>
        /// <returns>The updated transaction.</returns>
        Task<Transaction> MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions);

        /// <summary>
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction ids.
        /// </summary>
        /// <param name="depositIds">The deposit transaction ids.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds);

        /// <summary>Determines if the store contains suspended transactions.</summary>
        /// <returns><c>True</c> if the store contains suspended transaction and <c>false</c> otherwise.</returns>
        bool HasSuspended();

        /// <summary>
        /// Verifies that the transaction's input UTXO's have been reserved by the wallet.
        /// Also checks that an earlier transaction for the same deposit id does not exist.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <param name="checkSignature">Indicates whether to check the signature.</param>
        /// <returns><c>True</c> if all's well and <c>false</c> otherwise.</returns>
        bool ValidateTransaction(Transaction transaction, bool checkSignature = false);

        /// <summary>The tip of our chain when we last updated the store.</summary>
        ChainedHeader TipHashAndHeight { get; }

        /// <summary>The block height on the counter-chain for which the next list of deposits is expected.</summary>
        int NextMatureDepositHeight { get; }

        /// <summary>
        /// Gets the counter of the cross chain transfer for each available status
        /// </summary>
        /// <returns>The counter of the cross chain transfer for each <see cref="CrossChainTransferStatus"/> status</returns>
        Dictionary<CrossChainTransferStatus, int> GetCrossChainTransferStatusCounter();
    }
}
