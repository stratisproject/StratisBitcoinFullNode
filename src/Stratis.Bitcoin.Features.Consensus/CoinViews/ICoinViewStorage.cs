﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Database of UTXOs.
    /// </summary>
    public interface ICoinViewStorage
    {
        /// <summary>
        /// Retrieves the block hash of the current tip of the coinview.
        /// </summary>
        /// <returns>Block hash of the current tip of the coinview.</returns>
        Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Persists changes to the coinview storage.
        /// <para>
        /// This method is provided (in <paramref name="unspentOutputs"/> parameter) with information about all
        /// transactions that are either new or were changed in the new block. It is also provided with information
        /// (in <see cref="originalOutputs"/>) about the previous state of those transactions (if any),
        /// which is used for <see cref="Rewind"/> operation.
        /// </para>
        /// </summary>
        /// <param name="unspentOutputs">Information about the changes between the old block and the new block. An item in this list represents a list of all outputs
        /// for a specific transaction. If a specific output was spent, the output is <c>null</c>.</param>
        /// <param name="originalOutputs">Information about the previous state of outputs of transactions inside <paramref name="unspentOutputs"/>. If an item here is <c>null</c>,
        /// it means that the ouputs are newly created in the new block. If it is not <c>null</c>, it holds information about which outputs of the transaction were previously spent
        /// and which were not.</param>
        /// <param name="rewindDataCollection">List of rewind data items to persist.</param>
        Task PersistDataAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, List<RewindData> rewindDataCollection);

        /// <summary>
        /// Obtains information about unspent outputs for specific transactions and also retrieves information about the coinview's tip.
        /// </summary>
        /// <param name="txIds">Transaction identifiers for which to retrieve information about unspent outputs.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// Coinview tip's hash and information about unspent outputs in the requested transactions.
        /// <para>
        /// i-th item in <see cref="FetchCoinsResponse.UnspentOutputs"/> array is the information of the unspent outputs for i-th transaction in <paramref name="txIds"/>.
        /// If the i-th item of <see cref="FetchCoinsResponse.UnspentOutputs"/> is <c>null</c>, it means that there are no unspent outputs in the given transaction.
        /// </para>
        /// </returns>
        Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Rewinds the coinview to the last saved state.
        /// <para>
        /// This operation includes removing the UTXOs of the recent transactions
        /// and restoring recently spent outputs as UTXOs.
        /// </para>
        /// </summary>
        /// <returns>Hash of the block header which is now the tip of the rewound coinview.</returns>
        Task<uint256> Rewind();
    }
}
