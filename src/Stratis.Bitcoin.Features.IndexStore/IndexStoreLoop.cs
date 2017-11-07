using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreLoop : BlockStoreLoop
    {        
        public IndexStoreLoop(ConcurrentChain chain,
            IIndexRepository indexRepository,
            IndexSettings indexSettings,
            ChainState chainState,
            IndexBlockPuller blockPuller,
            IIndexStoreCache cache,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory, 
            IDateTimeProvider dateTimeProvider) :
            base(asyncLoopFactory, blockPuller, indexRepository, cache, chain, chainState, indexSettings, nodeLifetime, loggerFactory, dateTimeProvider)
        {
        }

        public override string StoreName { get { return "IndexStore"; } }
    }
}