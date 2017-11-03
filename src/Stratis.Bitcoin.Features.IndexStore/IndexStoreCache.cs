using System;
using Microsoft.Extensions.Caching.Memory;
using Stratis.Bitcoin.Features.BlockStore;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexStoreCache : IBlockStoreCache
    {
    }

    public class IndexStoreCache : BlockStoreCache, IIndexStoreCache
    {
        public IndexStoreCache(IIndexRepository indexRepository, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory) : 
            this(indexRepository, new MemoryCache(new MemoryCacheOptions()), dateTimeProvider, loggerFactory)
        {
        }

        public IndexStoreCache(IIndexRepository indexRepository, IMemoryCache memoryCache, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory) :
            base(indexRepository, memoryCache, dateTimeProvider, loggerFactory)
        {
        }

        public override BlockStoreCachePerformanceCounter BlockStoreCachePerformanceCounterFactory()
        {
            return new IndexStoreCachePerformanceCounter(this.dateTimeProvider);
        }
    }
}
