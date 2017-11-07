using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreSignaled: BlockStoreSignaled
    {
        public IndexStoreSignaled(IndexStoreLoop storeLoop, ConcurrentChain chain, IndexSettings indexSettings,
            ChainState chainState, IConnectionManager connection,
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory, IIndexRepository indexRepository, ILoggerFactory loggerFactory) :
            base(storeLoop, chain, indexSettings, chainState, connection, nodeLifetime, asyncLoopFactory, indexRepository, loggerFactory, "IndexStore")
        {
        }
    }
}
