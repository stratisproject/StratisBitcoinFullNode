using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Database of <see cref="ProvenBlockHeader"/>s.
    /// </summary>
    public interface IProvenBlockHeaderStore : IDisposable
    {
        /// <summary>
        /// Initialize the <see cref="ProvenBlockHeader"/> store.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task LoadAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Get a <see cref="ProvenBlockHeader"/> corresponding to a block.
        /// </summary>
        /// <param name="blockId">Id used to retrieve the <see cref="ProvenBlockHeader"/>.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns><see cref="ProvenBlockHeader"/> retrieved from the <see cref="ProvenBlockHeaderStore"/>.</returns>
        Task<ProvenBlockHeader> GetAsync(uint256 blockId, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Retrieves the <see cref="ProvenBlockHeader"/> of the current tip.
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns><see cref="ProvenBlockHeader"/></returns>
        Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds a <see cref="ProvenBlockHeader"/> to the internal concurrent dictionary. 
        /// </summary>
        /// <param name="provenBlockHeader"><see cref="ProvenBlockHeader"/> to add to the internal concurrent dictionary.</param>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SetAsync(ProvenBlockHeader provenBlockHeader, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Flushes the internal dictionary and saves any new items into the <see cref="ProvenBlockHeaderStore"/>. 
        /// </summary>
        /// <param name="cancellationToken">Cancellation that allows aborting the operation at any stage.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Returns items currently loaded in the <see cref="ProvenBlockHeaderStore"/>. 
        /// </summary>
        long Count { get; }
    }
}
