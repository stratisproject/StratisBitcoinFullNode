using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Structure made of a block and its chained header.
    /// </summary>
    public sealed class BlockPair
    {
        /// <summary>
        /// Creates instance of <see cref="BlockPair" />.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="chainedBlock">Chained header of the <paramref name="block"/>.</param>
        public BlockPair(Block block, ChainedBlock chainedBlock)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));
            Guard.Assert(block.GetHash() == chainedBlock.HashBlock);

            this.Block = block;
            this.ChainedBlock = chainedBlock;
        }

        public Block Block { get; private set; }

        public ChainedBlock ChainedBlock { get; private set; }
    }
}
