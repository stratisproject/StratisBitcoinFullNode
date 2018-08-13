using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.CoinViews
{
    /// <summary>
    /// Database of UTXOs.
    /// </summary>
    public interface ICoinView : IDisposable
    {
        /// <summary>
        /// Retrieves the block hash of the current tip of the coinview.
        /// </summary>
        /// <returns>Block hash of the current tip of the coinview.</returns>
        Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken));

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
