using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Interface <see cref="ProvenBlockHeader"/> provider.
    /// </summary>
    public interface IProvenBlockHeaderProvider : IDisposable
    {
        /// <summary>
        /// Get a <see cref="ProvenBlockHeader"/> corresponding to a block.
        /// </summary>
        /// <param name="blockHeight"> Height used to retrieve the <see cref="ProvenBlockHeader"/>.</param>
        /// <returns><see cref="ProvenBlockHeader"/> retrieved.</returns>
        Task<ProvenBlockHeader> GetAsync(int blockHeight);

        /// <summary>
        /// Retrieves <see cref="ProvenBlockHeader"/> items.
        /// </summary>
        /// <param name="fromBlockHeight"> Block height to start querying from.</param>
        /// <param name="toBlockHeight"> Block height to stop querying to.</param>
        /// <returns> A dictionary of <see cref="ProvenBlockHeader"/> items by block height key.</returns>
        Task<List<ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight);

        /// <summary>
        /// Height of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.
        /// </summary>
        HashHeightPair TipHashHeight { get; }
    }
}
