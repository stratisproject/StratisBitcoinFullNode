using System;
using System.Threading.Tasks;
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
        /// Initializes the <see cref="ProvenBlockHeaderStore"/>.
        /// <para>
        /// If the <see cref="storeTip"/> is <c>null</c> the store is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>The node crashed.</item>
        ///     <item>The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover it will walk back the <see cref= "ChainedHeader"/> until a common <see cref= "HashHeightPair"/> is found.
        /// Then the <see cref="ProvenBlockHeaderStore"/>'s <see cref="storeTip"/> will be set to that.
        /// </para>
        /// </summary>
        /// <param name="chainedHeader">Current <see cref="ChainedHeader"/> tip with all its ancestors.</param>
        Task InitializeAsync(ChainedHeader chainedHeader);

        /// <summary>
        /// Adds <see cref="ProvenBlockHeader"/> items to the pending batch. Ready for saving to disk.
        /// </summary>
        /// <param name="provenBlockHeader">A <see cref="ProvenBlockHeader"/> item to add.</param>
        /// <param name="newTip">Hash and height pair that represent the tip of <see cref="IProvenBlockHeaderStore"/>.</param>
        void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip);
    }
}
