using System;
using NBitcoin;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// </summary>
    public interface IChainState
    {
        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above <see cref="ConsensusTip"/>.</summary>
        ChainedHeader ConsensusTip { get; set; }

        /// <summary>The highest stored block in the repository or <c>null</c> if block store feature is not enabled.</summary>
        ChainedHeader BlockStoreTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        uint MaxReorgLength { get; set; }

        /// <summary>
        /// Check if a block is marked as invalid.
        /// </summary>
        /// <param name="hashBlock">The block hash to check.</param>
        /// <returns><c>true</c> if the block is marked as invalid.</returns>
        bool IsMarkedInvalid(uint256 hashBlock);

        /// <summary>
        /// Marks a block as invalid. This is used to prevent DOS attacks as the next time the block is seen, it is not processed again.
        /// </summary>
        /// <param name="hashBlock">The block hash to mark as invalid.</param>
        /// <param name="rejectedUntil">Time in UTC after which the block is no longer considered as invalid, or <c>null</c> if the block is to be considered invalid forever.</param>
        void MarkBlockInvalid(uint256 hashBlock, DateTime? rejectedUntil = null);
    }

    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// The data are provided by different components and the chaine state is a mechanism that allows
    /// these components to share that data without creating extra dependencies.
    /// </summary>
    public class ChainState : IChainState
    {
        /// <summary>Store of block header hashes that are to be considered invalid.</summary>
        private readonly IInvalidBlockHashStore invalidBlockHashStore;

        /// <inheritdoc />
        public ChainedHeader ConsensusTip { get; set; }

        /// <inheritdoc />
        public ChainedHeader BlockStoreTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        public uint MaxReorgLength { get; set; }

        /// <summary>
        /// Initialize instance of the object.
        /// </summary>
        /// <param name="invalidBlockHashStore">Store of block header hashes that are to be considered invalid.</param>
        public ChainState(IInvalidBlockHashStore invalidBlockHashStore)
        {
            this.invalidBlockHashStore = invalidBlockHashStore;
        }

        /// <inheritdoc/>
        public bool IsMarkedInvalid(uint256 hashBlock)
        {
            return this.invalidBlockHashStore.IsInvalid(hashBlock);
        }

        /// <inheritdoc/>
        public void MarkBlockInvalid(uint256 hashBlock, DateTime? rejectedUntil = null)
        {
            this.invalidBlockHashStore.MarkInvalid(hashBlock, rejectedUntil);
        }
    }
}
