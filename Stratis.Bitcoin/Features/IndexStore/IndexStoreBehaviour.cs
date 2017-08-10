using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexStoreBehavior: IBlockStoreBehavior
    {

    }

    public class IndexStoreBehavior : BlockStoreBehavior
    {
        public IndexStoreBehavior(ConcurrentChain chain, IndexRepository blockRepository, IndexStoreCache blockStoreCache, ILogger logger) : 
            this(chain, blockRepository as IIndexRepository, blockStoreCache as IIndexStoreCache, logger)
        {
        }

        public IndexStoreBehavior(ConcurrentChain chain, IIndexRepository blockRepository, IIndexStoreCache blockStoreCache, ILogger logger) :
            base(chain, blockRepository as Features.BlockStore.IBlockRepository, blockStoreCache as Features.BlockStore.IBlockStoreCache, logger)
        {
        }
    }
}
