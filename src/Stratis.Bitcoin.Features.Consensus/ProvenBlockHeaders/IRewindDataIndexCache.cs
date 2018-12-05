using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Rewind data index data store, where index structure consists of a key-value storage where key is a TxId + N (N is an index of output in a transaction)
    /// and value is a rewind data index. This data structure will always contain as many entries as there are rewind data instances in the database (currently
    /// we do not delete old rewind data that is no longer needed but after the issue #5 (https://github.com/stratisproject/StratisBitcoinFullNode/issues/5)
    /// is fixed we should also make sure that old fork point data is deleted as well).
    /// </summary>
    public interface IRewindDataIndexCache
    {
        /// <summary>
        /// Initializes the rewind data index store by loading rewind data from the cache or dbreeze for the last 500 blocks.
        /// </summary>
        /// <param name="tipHeight">The chain tip height.</param>
        /// <param name="coinView">The coin view to be used for getting rewind data.</param>
        Task InitializeAsync(int tipHeight, ICoinView coinView);

        /// <summary>
        /// Removes the last rewind data index and add one instead.
        /// </summary>
        /// <param name="tipHeight">The chain tip height.</param>
        /// <param name="coinView">The coin view to be used for getting rewind data.</param>
        Task Remove(int tipHeight, ICoinView coinView);

        /// <summary>
        /// Stores all rewind data index from the cache to a disk and clears cache.
        /// </summary>
        /// <param name="tipHeight">The current block height.</param>
        void Flush(int tipHeight);

        /// <summary>
        /// Saves rewind index data to cache.
        /// </summary>
        /// <param name="indexData">The rewind index data, where key is TxId + N and value is a height of the rewind data.</param>
        void Save(Dictionary<string, int> indexData);

        /// <summary>
        /// Gets rewind data index from the cache (or if not found from the disk) by tx id and output index.
        /// </summary>
        /// <param name="transactionId">The transaction id.</param>
        /// <param name="transactionOutputIndex">Index of the transaction output.</param>
        /// <returns>If found, rewind data index, else null.</returns>
        int? Get(uint256 transactionId, int transactionOutputIndex);
    }
}
