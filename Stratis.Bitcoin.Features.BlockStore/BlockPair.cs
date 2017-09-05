using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public sealed class BlockPair
    {
        /// <summary>
        /// Construct BlockPair with a Block and ChainedBlock instance to ensure that its valid
        /// </summary>
        public BlockPair(Block block, ChainedBlock chainedBlock)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.Block = block;
            this.ChainedBlock = chainedBlock;
        }

        public Block Block { get; private set; }
        public ChainedBlock ChainedBlock { get; private set; }
    }
}