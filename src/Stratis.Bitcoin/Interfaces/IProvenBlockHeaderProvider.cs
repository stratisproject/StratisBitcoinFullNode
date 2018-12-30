using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Interfaces
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
        /// Height of the block which is currently the tip of the <see cref="ProvenBlockHeader"/>.
        /// </summary>
        HashHeightPair TipHashHeight { get; }
    }
}
