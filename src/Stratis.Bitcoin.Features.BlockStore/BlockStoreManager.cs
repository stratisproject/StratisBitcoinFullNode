using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        public IBlockRepository BlockRepository { get; }

        public BlockStore BlockStore { get; }

        public BlockStoreManager(IBlockRepository blockRepository, BlockStore blockStore)
        {
            this.BlockRepository = blockRepository;
            this.BlockStore = blockStore;
        }
    }
}
