using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreSignaled : BlockStoreSignaled
    {
        public IndexStoreSignaled(IndexStoreLoop storeLoop, ConcurrentChain chain, IndexSettings indexSettings,
            ChainState chainState, IConnectionManager connection,
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory, IBlockRepository blockRepository, ILoggerFactory loggerFactory) :
            base(storeLoop, chain, indexSettings, chainState, connection, nodeLifetime, asyncLoopFactory, blockRepository, loggerFactory, "IndexStore")
        {
        }
    }
}
