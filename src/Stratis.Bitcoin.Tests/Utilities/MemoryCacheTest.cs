using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class MemoryCacheTest
    {
        [Fact]
        public void CacheCanCompact()
        {
            var cache = new MemoryCache<int, string>(100, 0.5);
            
            for (int i = 0; i < 200; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            Assert.Equal(50, cache.Count);
        }

        [Fact]
        public void CacheKeepsMostRecentlyAddedItemsOnCompactionIfNoneWereUsed()
        {
            var cache = new MemoryCache<int, string>(10, 0.5);
            
            for (int i = 0; i < 10; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            for (int i = 0; i < 10; ++i)
            {
                bool success = cache.TryGetValue(i, out string value);

                if (i < 5)
                {
                    Assert.False(success);
                }
                else
                {
                    Assert.True(success);
                }
            }

            Assert.Equal(5, cache.Count);
        }

        [Fact]
        public void CanManuallyRemoveItemsFromTheCache()
        {
            var cache = new MemoryCache<int, string>(10, 0.5);
            
            for (int i = 0; i < 5; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            for (int i = 0; i < 3; ++i)
            {
                cache.Remove(i);
            }

            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void CacheKeepsMostRecentlyUsedItems()
        {
            var cache = new MemoryCache<int, string>(11, 0.5);
            
            // Add 10 items.
            for (int i = 0; i < 10; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            // Use first 3 items.
            for (int i = 0; i < 3; ++i)
            {
                cache.TryGetValue(i, out string unused);
            }

            // Add 11th item to trigger compact.
            cache.AddOrUpdate(10, 10 + "VALUE");

            // Cache should have 0,1,2,8,9,10
            for (int i = 0; i < 11; ++i)
            {
                bool success = cache.TryGetValue(i, out string unused);

                if (i < 3 || i > 7)
                    Assert.True(success);
                else
                    Assert.False(success);
            }
        }
    }
}
