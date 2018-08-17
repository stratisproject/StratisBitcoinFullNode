using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Database of UTXOs.
    /// </summary>
    public interface ICoinViewStorage : ICoinView
    {
        /// <summary>
        /// Persists changes to the coinview storage.
        /// <para>
        /// This method is provided (in <paramref name="unspentOutputs"/> parameter) with information about all
        /// transactions that are either new or were changed in the new block. It is also provided with a list of rewind data items,
        /// which are used for <see cref="ICoinView.Rewind"/> operation.
        /// </para>
        /// </summary>
        /// <param name="unspentOutputs">Information about the changes between the old block and the new block. An item in this list represents a list of all outputs
        /// for a specific transaction. If a specific output was spent, the output is <c>null</c>.</param>
        /// <param name="rewindDataCollection">List of rewind data items to persist.</param>
        /// <param name="oldBlockHash">Old block hash.</param>
        /// <param name="nextBlockHash">Next block hash.</param>
        Task PersistDataAsync(IList<UnspentOutputs> unspentOutputs, List<RewindData> rewindDataCollection, uint256 oldBlockHash, uint256 nextBlockHash);

        /// <summary>
        /// Initializes the database tables used by the coinview.
        /// </summary>
        Task InitializeAsync();

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        BackendPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Retrieves POS blocks information from the database.
        /// </summary>
        /// <param name="blocklist">List of partially initialized POS block information that is to be fully initialized with the values from the database.</param>
        Task GetStakeAsync(IEnumerable<StakeItem> blocklist);

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        Task PutStakeAsync(IEnumerable<StakeItem> stakeEntries);
    }
}
