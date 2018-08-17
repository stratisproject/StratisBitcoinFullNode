using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Database of UTXOs.
    /// </summary>
    public interface ICachedCoinView : ICoinView
    {
        /// <summary>
        /// Persists changes to the coinview when a new block
        /// (hash <paramref name="currentBlock"/>) is added and becomes the new tip of the coinview.
        /// <para>
        /// This method is provided (in <paramref name="unspentOutputs"/> parameter) with information about all
        /// transactions that are either new or were changed in the new block. It is also provided with information
        /// in original outputs about the previous state of those transactions (if any),
        /// which is used for <see cref="ICoinView.Rewind"/> operation.
        /// </para>
        /// </summary>
        /// <param name="unspentOutputs">Information about the changes between the old block and the new block. An item in this list represents a list of all outputs
        /// for a specific transaction. If a specific output was spent, the output is <c>null</c>.</param>
        /// <param name="currentBlock">Block of the current tip of the coinview.</param>
        Task AddRewindDataAsync(IList<UnspentOutputs> unspentOutputs, ChainedHeader currentBlock);

        /// <summary>
        /// Initializes this instance.
        /// </summary>
         void Initialize();

        /// <summary>Statistics of hits and misses in the cache.</summary>
        CachePerformanceCounter PerformanceCounter { get; set; }

        /// <summary>Number of items in the cache.</summary>
        int CacheEntryCount { get; }
    }
}
