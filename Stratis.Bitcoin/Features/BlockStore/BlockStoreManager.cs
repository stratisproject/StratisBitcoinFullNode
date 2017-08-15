using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreManager
    {
        private readonly ConcurrentChain chain;
        private readonly IConnectionManager connection;
        public IBlockRepository BlockRepository { get; } // public for testing
        public BlockStoreLoop BlockStoreLoop { get; } // public for testing

        private readonly IDateTimeProvider dateTimeProvider;
        private readonly NodeSettings nodeArgs;
        public ChainState ChainState { get; }

        public BlockStoreManager(ConcurrentChain chain, IConnectionManager connection, IBlockRepository blockRepository,
            IDateTimeProvider dateTimeProvider, NodeSettings nodeArgs, ChainState chainState, BlockStoreLoop blockStoreLoop)
        {
            this.chain = chain;
            this.connection = connection;
            this.BlockRepository = blockRepository;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeArgs = nodeArgs;
            this.ChainState = chainState;
            this.BlockStoreLoop = blockStoreLoop;
        }
    }
}