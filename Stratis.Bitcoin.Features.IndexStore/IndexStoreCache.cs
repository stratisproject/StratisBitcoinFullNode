using System;
using Microsoft.Extensions.Caching.Memory;
using Stratis.Bitcoin.Features.BlockStore;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexStoreCache : IBlockStoreCache
    {
    }

    public class IndexStoreCache : BlockStoreCache, IIndexStoreCache
    {
        public IndexStoreCache(IIndexRepository indexRepository, ILoggerFactory loggerFactory) : this(indexRepository, new MemoryCache(new MemoryCacheOptions()), loggerFactory)
        {
        }

        public IndexStoreCache(IIndexRepository indexRepository, IMemoryCache memoryCache, ILoggerFactory loggerFactory)
            :base(indexRepository, memoryCache, loggerFactory)
        {
        }

        public override BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new IndexStoreCachePerformanceCounter();
        }
    }
}
