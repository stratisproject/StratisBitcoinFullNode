using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface for database <see cref="ProvenBlockHeader"></see> repository.
    /// </summary>
    public interface IProvenBlockHeaderRepository : IDisposable
    {
        /// <summary>Loads <see cref="ProvenBlockHeader"/> items from the database.</summary>
        /// <param name="blockHash">BlockId to initial the database with.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync(uint256 blockHash = null);

        /// <summary> Retrieves the block hash of the current <see cref="ProvenBlockHeader"/> tip.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<uint256> GetTipHashAsync();

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items from the database.
        /// </summary>
        /// <param name="blockIds">Block hashes used to query the database.</param>
        /// <returns>Proof of stake items which include the returned <see cref="ProvenBlockHeader"/> from disk.</returns>
        Task<List<StakeItem>> GetAsync(IEnumerable<uint256> blockIds);

        /// <summary>Persists <see cref="ProvenBlockHeader"/> items to the database.</summary>
        /// <param name="stakeItems">Proof of stake items which includes <see cref="ProvenBlockHeader"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task PutAsync(IEnumerable<StakeItem> stakeItems);

        /// <summary>
        /// Determine if a <see cref="ProvenBlockHeader"/> already exists in the database.
        /// </summary>
        /// <param name="blockId">The block hash.</param>
        /// <returns><c>true</c> if the block hash can be found in the database, otherwise return <c>false</c>.</returns>
        Task<bool> ExistsAsync(uint256 blockId);

        /// <summary>
        /// Delete <see cref="ProvenBlockHeader"/> items.
        /// </summary>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        /// <param name="blockIds">List of all block hashes to be deleted.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task DeleteAsync(uint256 newTip, List<uint256> blockIds);
    }
}
