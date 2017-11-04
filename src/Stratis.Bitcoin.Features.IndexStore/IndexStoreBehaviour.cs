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
        public IndexStoreBehavior(ConcurrentChain chain, IIndexRepository indexRepository, IIndexStoreCache indexStoreCache, ILoggerFactory loggerFactory) :
            base(chain, indexRepository, indexStoreCache, loggerFactory)
        {
        }
    }
}
