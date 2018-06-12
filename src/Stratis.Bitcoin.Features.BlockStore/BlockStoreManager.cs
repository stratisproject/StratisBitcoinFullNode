namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        public IBlockRepository BlockRepository { get; }

        public BlockStoreQueue BlockStoreQueue { get; }

        public BlockStoreManager(IBlockRepository blockRepository, BlockStoreQueue blockStoreQueue)
        {
            this.BlockRepository = blockRepository;
            this.BlockStoreQueue = blockStoreQueue;
        }
    }
}
