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
                cache.CreateEntry(i, i + "VALUE");
            }

            Assert.Equal(50, cache.Count);
        }

        [Fact]
        public void CacheKeepsMostRecentItemsOnCompaction()
        {
            var cache = new MemoryCache<int, string>(10, 0.5);

            for (int i = 0; i < 10; ++i)
            {
                cache.CreateEntry(i, i + "VALUE");
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
        }

        [Fact]
        public void CanManuallyRemoveItemsFromTheCache()
        {
            var cache = new MemoryCache<int, string>(10, 0.5);

            for (int i = 0; i < 5; ++i)
            {
                cache.CreateEntry(i, i + "VALUE");
            }

            for (int i = 0; i < 3; ++i)
            {
                cache.Remove(i);
            }

            Assert.Equal(2, cache.Count);
        }
    }
}
