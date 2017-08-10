using System;
using Microsoft.Extensions.Caching.Memory;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexStoreCache : IBlockStoreCache
    {
    }

    public class IndexStoreCache : BlockStoreCache, IIndexStoreCache
    {
        public IndexStoreCache(IndexRepository indexRepository) : this(indexRepository, new MemoryCache(new MemoryCacheOptions()))
        {
        }

        public IndexStoreCache(IIndexRepository indexRepository, IMemoryCache memoryCache)
            :base(indexRepository, memoryCache)
        {
        }

        public override BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new IndexStoreCachePerformanceCounter();
        }
    }
}
