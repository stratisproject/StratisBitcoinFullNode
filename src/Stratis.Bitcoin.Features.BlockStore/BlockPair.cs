using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// BlockPair is a structure made of a block and a ChainedBlock that represents the block.
    /// </summary>
    public sealed class BlockPair
    {
        /// <summary>
        /// Creates instance of <see cref="BlockPair"/>.
        /// </summary>
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
