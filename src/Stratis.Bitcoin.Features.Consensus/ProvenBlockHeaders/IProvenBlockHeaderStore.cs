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
        /// Initializes the <see cref="IProvenBlockHeaderStore"/>.
        /// <para>
        /// If the <see cref="ProvenBlockHeaderStore.TipHashHeight"/> is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>The node crashed.</item>
        ///     <item>The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover it will overwrite the <see cref= "ChainedHeader"/> tip <see cref= "HashHeightPair"/>.
        /// </para>
        /// </summary>
        /// <param name="chainedHeader"><see cref="ChainedHeader"/> consensus tip after <see cref="Bitcoin.Consensus.IConsensusManager"/> initialization.</param>
        /// <exception cref="ProvenBlockHeaderException">
        /// Thrown when :
        /// <list type="bullet">
        /// <item>
        /// <term>Missing.</term>
        /// <description>When the <see cref="ProvenBlockHeader"/> selected by <see cref="ChainedHeader.Height"/> does not exist in the database.</description>
        /// </item>
        /// <item>
        /// <term>Block hash mismatch.</term>
        /// <description>The <see cref="ChainedHeader"/> tip hash does not match the latest <see cref="ProvenBlockHeader"/> hash saved to disk.</description>
        /// </item>
        /// </list>
        /// </exception>
        Task InitializeAsync(ChainedHeader chainedHeader);

        /// <summary>
        /// Adds <see cref="ProvenBlockHeader"/> items to the pending batch. Ready for saving to disk.
        /// </summary>
        /// <param name="provenBlockHeader">A <see cref="ProvenBlockHeader"/> item to add.</param>
        /// <param name="newTip">Hash and height pair that represent the tip of <see cref="IProvenBlockHeaderStore"/>.</param>
        void AddToPendingBatch(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip);
    }
}
