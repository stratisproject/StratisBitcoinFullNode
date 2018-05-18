using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        public IBlockRepository BlockRepository { get; }

        public BlockStore BlockStore { get; }

        public IChainState ChainState { get; }

        public BlockStoreManager(IBlockRepository blockRepository, IChainState chainState, BlockStore blockStore)
        {
            this.BlockRepository = blockRepository;
            this.ChainState = chainState;
            this.BlockStore = blockStore;
        }
    }
}
