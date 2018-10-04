using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Cache layer for <see cref="ProvenBlockHeaderStore"/>s.
    /// </summary>
    public interface IProvenBlockHeaderStore : IProvenBlockHeaderProvider
    {
        /// <summary>
        /// Adds <see cref="ProvenBlockHeader"/> items to the pending batch.  Ready for saving to disk.
        /// </summary>
        /// <param name="provenBlockHeader">A <see cref="ProvenBlockHeader"/> item to add.</param>
        /// <param name="newTip">Block hash and height pair associated against the <see cref="ProvenBlockHeader"/> item.</param>
        void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip);
    }
}
