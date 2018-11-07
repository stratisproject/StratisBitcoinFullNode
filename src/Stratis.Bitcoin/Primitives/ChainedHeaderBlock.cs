using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Primitives
{
    /// <summary>
    /// Structure made of a block and its chained header.
    /// </summary>
    public sealed class ChainedHeaderBlock : IHeaderSetter
    {
        /// <summary>The block.</summary>
        public Block Block { get; private set; }

        /// <summary>Chained header of the <see cref="Block"/>.</summary>
        public ChainedHeader ChainedHeader { get; private set; }

        /// <summary>
        /// Creates instance of <see cref="ChainedHeaderBlock" />.
        /// </summary>
        /// <param name="block">The block can be <c>null</c>.</param>
        /// <param name="chainedHeader">Chained header of the <paramref name="block"/>.</param>
        public ChainedHeaderBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            this.Block = block;
            this.ChainedHeader = chainedHeader;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ChainedHeader.ToString();
        }

        /// <summary>
        /// Update the <see cref="ChainedHeader" /> and the <see cref="Block" /> (if not null) with a new provided header.
        /// </summary>
        /// <param name="newHeader">The new header to set.</param>
        /// <remarks>Use this method very carefully because it could cause race conditions if used at the wrong moment.</remarks>
        void IHeaderSetter.SetHeader(BlockHeader newHeader)
        {
            Guard.NotNull(newHeader, nameof(newHeader));

            ((IHeaderSetter)this.ChainedHeader)?.SetHeader(newHeader);
            ((IHeaderSetter)this.Block)?.SetHeader(newHeader);
        }
    }
}
