using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface to insert and retrieve <see cref="RewindData"/> index items from the database repository.
    /// </summary>
    public interface IRewindDataIndexRepository
    {
        /// <summary>
        /// Persists key value pair for the rewind data index item.
        /// </summary>
        /// <param name="items">Rewind data index items.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PutAsync(IDictionary<string, int> items, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Get a rewind data index corresponding to a given transaction id + output index.
        /// </summary>
        /// <param name="key">Transaction id + N (N is an index of output in a transaction).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>If key was found, then index of the rewind data, otherwise null.</returns>
        Task<int?> GetAsync(string key, CancellationToken cancellationToken = default(CancellationToken));
    }
}
