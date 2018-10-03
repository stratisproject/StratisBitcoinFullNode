using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <summary>
    /// Cache layer for <see cref="ProvenBlockHeader"/>s.
    /// </summary>
    public interface IProvenBlockHeaderStore : IProvenBlockHeaderProvider, IDisposable
    {
        /// <summary>
        /// Adds a <see cref="ProvenBlockHeader"/> to pending batch concurrent dictionary.
        /// </summary>
        /// <param name="provenBlockHeader">A <see cref="ProvenBlockHeader"/> item to add.</param>
        /// <param name="newTip">Block hash and height pair associated against the <see cref="ProvenBlockHeader"/> item.</param>
        void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip);
    }
}
