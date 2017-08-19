using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;
using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreSignaled: BlockStoreSignaled
    {
        public IndexStoreSignaled(IndexStoreLoop storeLoop, ConcurrentChain chain, NodeSettings nodeArgs,
            ChainState chainState, IConnectionManager connection,
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory, IBlockRepository blockRepository, ILoggerFactory loggerFactory) :
            base(storeLoop, chain, nodeArgs, chainState, connection, nodeLifetime, asyncLoopFactory, blockRepository, loggerFactory, "IndexStore")
        {
        }
    }
}
