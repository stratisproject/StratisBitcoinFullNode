using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreLoop : BlockStoreLoop
    {
        public IndexStoreLoop(ConcurrentChain chain,
            IndexRepository indexRepository,
            NodeSettings nodeArgs,
            ChainState chainState,
            IndexBlockPuller blockPuller,
            IndexStoreCache cache,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory):
            base(asyncLoopFactory, blockPuller, indexRepository, cache, chain, chainState, nodeArgs, nodeLifetime, loggerFactory, "IndexStore") 
        {
            nodeArgs.Store.TxIndex = true;
        }

        protected override void SetHighestPersistedBlock(ChainedBlock block)
        {
            this.ChainState.HighestIndexedBlock = block;
        }
    }
}
