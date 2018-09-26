using System;
using System.Collections.Generic;
using System.Threading;
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
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task InitializeAsync(uint256 blockHash = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary> Retrieves the block hash of the current <see cref="ProvenBlockHeader"/> tip.</summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items from the database.
        /// </summary>
        /// <param name="stakeItems">Proof of stake items which includes <see cref="ProvenBlockHeader"/>.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task GetAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Persists <see cref="ProvenBlockHeader"/> items to the database.</summary>
        /// <param name="stakeItems">Proof of stake items which includes <see cref="ProvenBlockHeader"/>.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task PutAsync(IEnumerable<StakeItem> stakeItems, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Determine if a <see cref="ProvenBlockHeader"/> already exists in the database.
        /// </summary>
        /// <param name="blockId">The block hash.</param>
        /// <returns><c>true</c> if the block hash can be found in the database, otherwise return <c>false</c>.</returns>
        Task<bool> ExistsAsync(uint256 blockId);
    }
}
