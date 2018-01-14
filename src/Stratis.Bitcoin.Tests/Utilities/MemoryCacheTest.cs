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
        public void CacheDoesNotExceedMaxItemsLimit()
        {
            var cache = new MemoryCache<int, string>(100);
            
            for (int i = 0; i < 200; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            Assert.Equal(100, cache.Count);
        }

        [Fact]
        public void CacheKeepsMostRecentlyAddedItemsNoneWereUsed()
        {
            var cache = new MemoryCache<int, string>(10);
            
            for (int i = 0; i < 100; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            for (int i = 90; i < 100; ++i)
            {
                bool success = cache.TryGetValue(i, out string value);

                Assert.True(success);
            }

            Assert.Equal(10, cache.Count);
        }

        [Fact]
        public void CanManuallyRemoveItemsFromTheCache()
        {
            var cache = new MemoryCache<int, string>(10);
            
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
            var cache = new MemoryCache<int, string>(10);
            
            for (int i = 0; i < 15; ++i)
            {
                cache.AddOrUpdate(i, i + "VALUE");

                if (i == 8)
                {
                    // Use first 3 items.
                    for (int k = 0; k < 3; ++k)
                    {
                        cache.TryGetValue(k, out string unused);
                    }
                }
            }
            
            // Cache should have 0-2 & 8-14.
            for (int i = 0; i < 15; ++i)
            {
                bool success = cache.TryGetValue(i, out string unused);

                if (i < 3 || i > 7)
                    Assert.True(success);
                else
                    Assert.False(success);
            }

            Assert.Equal(10, cache.Count);
        }
    }
}
