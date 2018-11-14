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
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction ids.
        /// </summary>
        /// <param name="depositIds">The deposit transaction ids.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds);

        /// <summary>
        /// Records the mature deposits from <see cref="NextMatureDepositHeight"/> on the counter-chain.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// The caller should check that <see cref="NextMatureDepositHeight"/> is a height on the
        /// counter-chain which would contain mature deposits.
        /// </summary>
        /// <param name="crossChainTransfers">The deposit transactions.</param>
        /// <remarks>
        /// When building the list of transfers the caller should first use <see cref="GetAsync"/>
        /// to check whether the transfer already exists without the deposit information and
        /// then provide the updated object in this call.
        /// The caller must also ensure the transfers passed to this call all have a
        /// <see cref="ICrossChainTransfer.Status"/> of <see cref="CrossChainTransferStatus.Partial"/>.
        /// </remarks>
        Task RecordLatestMatureDepositsAsync(IEnumerable<ICrossChainTransfer> crossChainTransfers);

        /// <summary>
        /// Uses the information contained in our chain's blocks to update the store.
        /// Sets the <see cref="CrossChainTransferStatus.SeenInBlock"/> status for transfers
        /// identified in the blocks.
        /// </summary>
        /// <param name="newTip">The new <see cref="ChainTip"/>.</param>
        /// <param name="blocks">The blocks used to update the store. Must be sorted by ascending height leading up to the new tip.</param>
        Task PutAsync(HashHeightPair newTip, List<Block> blocks);

        /// <summary>
        /// Used to handle reorg (if required) and revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>. Also returns a flag to indicate whether we are behind the current tip.
        /// The caller can use <see cref="PutAsync"/> to supply additional blocks if we are behind the tip.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.
        /// </returns>
        Task<bool> RewindIfRequiredAsync();

        /// <summary>
        /// Attempts to synchronizes the store with the chain.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if the store is in sync or <c>false</c> otherwise.
        /// </returns>
        Task<bool> SynchronizeAsync();

        /// <summary>
        /// Updates partial transactions in the store with signatures obtained from the passed transactions.
        /// The <see cref="CrossChainTransferStatus.FullySigned"/> status is set on fully signed transactions.
        /// </summary>
        /// <param name="depositId">The deposit transaction to update.</param>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions);

        /// <summary>
        /// Sets the cross-chaintransfer status associated with the rejected transaction to to <see cref="CrossChainTransferStatus.Rejected"/>.
        /// </summary>
        /// <param name="transaction">The transaction that was rejected.</param>
        Task SetRejectedStatusAsync(Transaction transaction);

        /// <summary>
        /// Returns all fully signed transactions. The caller is responsible for checking the memory pool and
        /// not re-broadcasting transactions unneccessarily.
        /// </summary>
        /// <returns>An array of fully signed transactions.</returns>
        Task<Transaction[]> GetTransactionsToBroadcastAsync();

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