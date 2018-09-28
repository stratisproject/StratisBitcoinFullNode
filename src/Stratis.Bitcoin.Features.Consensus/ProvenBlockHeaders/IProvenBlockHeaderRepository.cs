using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface for database <see cref="ProvenBlockHeader"></see> repository.
    /// </summary>
    public interface IProvenBlockHeaderRepository : IDisposable
    {
        /// <summary>
        /// Initializes <see cref="ProvenBlockHeader"/> items database.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items from the database.
        /// </summary>
        /// <param name="fromBlockHeight">Block height to start range.</param>
        /// <param name="toBlockHeight">Block height end range.</param>
        /// <returns>A dictionary of <see cref="ProvenBlockHeader"/> items by block height.</returns>
        Task<Dictionary<int, ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight);

        /// <summary>
        /// Retrieves a <see cref="ProvenBlockHeader"/> item from the database.
        /// </summary>
        /// <param name="blockHeight">Block height to query.</param>
        /// <returns>A <see cref="ProvenBlockHeader"/> item.</returns>
        Task<ProvenBlockHeader> GetAsync(int blockHeight);

        /// <summary>
        /// Persists <see cref="ProvenBlockHeader"/> items to the database.
        /// </summary>
        /// <param name="provenBlockHeaders">List of <see cref="ProvenBlockHeader"/> items.</param>
        /// <param name="newTip">Block hash and height tip.</param>
        Task PutAsync(List<ProvenBlockHeader> provenBlockHeaders, HashHeightPair newTip);

        /// <summary>
        /// Retrieves the block hash and height of the current <see cref="ProvenBlockHeader"/> tip.
        /// </summary>
        Task<HashHeightPair> GetTipHashHeightAsync();

        /// <summary>
        /// Determine if a <see cref="ProvenBlockHeader"/> already exists in the database.
        /// </summary>
        /// <param name="blockHeight">The block height.</param>
        /// <returns><c>true</c> if the block height can be found in the database, otherwise return <c>false</c>.</returns>
        Task<bool> ExistsAsync(int blockHeight);

        /// <summary>
        /// Delete <see cref="ProvenBlockHeader"/> items.
        /// </summary>
        /// <param name="newTip">Block hash and height pair of the new repository's tip.</param>
        /// <param name="blockHeights">List of all block heights to be deleted.</param>
        Task DeleteAsync(HashHeightPair newTip, List<int> blockHeights);        
    }
}
