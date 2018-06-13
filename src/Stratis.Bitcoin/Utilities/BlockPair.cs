using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Structure made of a block and its chained header.
    /// </summary>
    public sealed class BlockPair
    {
        /// <summary>The block.</summary>
        public Block Block { get; private set; }

        /// <summary>Chained header of the <see cref="Block"/>.</summary>
        public ChainedHeader ChainedHeader { get; private set; }

        /// <summary>
        /// Creates instance of <see cref="BlockPair" />.
        /// </summary>
        /// <param name="block">The block, the block can be <c>null</c>.</param>
        /// <param name="chainedHeader">Chained header of the <paramref name="block"/>.</param>
        public BlockPair(Block block, ChainedHeader chainedHeader)
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
    }
}
