using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Interface for interacting with the cross-chain transfer database.
    /// </summary>
    public interface ICrossChainTransferStore : IDisposable
    {
        /// <summary>
        /// Initializes the cross-chain-transfer store.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Starts the cross-chain-transfer store.
        /// </summary>
        void Start();

        /// <summary>
        /// Records the mature deposits from <see cref="NextMatureDepositHeight"/> on the counter-chain.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// </summary>
        /// <param name="deposits">The deposits.</param>
        /// <remarks>
        /// The transfers are set to <see cref="CrossChainTransfer.Status"/> of <see cref="CrossChainTransferStatus.Partial"/>
        /// or <see cref="CrossChainTransferStatus.Rejected"/> depending on whether enough funds are available in the federation wallet.
        /// New partial transactions are recorded in the wallet to ensure that future transactions will not
        /// attempt to re-use UTXO's.
        /// </remarks>
        Task RecordLatestMatureDepositsAsync(IDeposit[] deposits);

        /// <summary>
        /// Returns transactions by status. Orders the results by UTXO selection order.
        /// </summary>
        /// <param name="status">The status to get the transactions for.</param>
        /// <returns>An array of transactions.</returns>
        Task<Dictionary<uint256, Transaction>> GetTransactionsByStatusAsync(CrossChainTransferStatus status);

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

        Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions);

        /// <summary>
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction ids.
        /// </summary>
        /// <param name="depositIds">The deposit transaction ids.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds);

        /// <summary>
        /// The tip of our chain when we last updated the store.
        /// </summary>
        HashHeightPair TipHashAndHeight { get; }

        /// <summary>
        /// The block height on the counter-chain for which the next list of deposits is expected.
        /// </summary>
        int NextMatureDepositHeight { get; }
    }
}