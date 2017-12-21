using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        public IBlockRepository BlockRepository { get; }

        public BlockStoreLoop BlockStoreLoop { get; }

        public ChainState ChainState { get; }

        public BlockStoreManager(IBlockRepository blockRepository, ChainState chainState, BlockStoreLoop blockStoreLoop)
        {
            this.BlockRepository = blockRepository;
            this.ChainState = chainState;
            this.BlockStoreLoop = blockStoreLoop;
        }
    }
}
