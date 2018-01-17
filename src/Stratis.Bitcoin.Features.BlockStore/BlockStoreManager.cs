using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        public IBlockRepository BlockRepository { get; }

        public BlockStoreLoop BlockStoreLoop { get; }

        public IChainState ChainState { get; }

        public BlockStoreManager(IBlockRepository blockRepository, IChainState chainState, BlockStoreLoop blockStoreLoop)
        {
            this.BlockRepository = blockRepository;
            this.ChainState = chainState;
            this.BlockStoreLoop = blockStoreLoop;
        }
    }
}
