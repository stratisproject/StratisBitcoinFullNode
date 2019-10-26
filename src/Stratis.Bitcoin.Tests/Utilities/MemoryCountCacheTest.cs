using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class MemoryCountCacheTest
    {
        [Fact]
        public void CacheDoesNotExceedMaxItemsLimit()
        {
            int maxItemsCount = 100;

            var cache = new MemoryCountCache<int, string>(maxItemsCount);

            for (int i = 0; i < maxItemsCount * 2; i++)
            {
                cache.AddOrUpdate(i, RandomUtils.GetInt32().ToString());
            }

            Assert.Equal(maxItemsCount, cache.Count);
        }

        [Fact]
        [Trait("Unstable", "True")]
        public void CacheKeepsMostRecentlyAddedItemsNoneWereUsed()
        {
            int maxItemsCount = 10;
            int itemsCountToAdd = 100;

            var cache = new MemoryCountCache<int, string>(maxItemsCount);

            for (int i = 0; i < itemsCountToAdd; i++)
            {
                cache.AddOrUpdate(i, RandomUtils.GetInt32().ToString());
            }

            for (int i = itemsCountToAdd - maxItemsCount; i < itemsCountToAdd; i++)
            {
                bool success = cache.TryGetValue(i, out string value);

                Assert.True(success);
            }

            Assert.Equal(maxItemsCount, cache.Count);
        }

        [Fact]
        public void CanManuallyRemoveItemsFromTheCache()
        {
            var cache = new MemoryCountCache<int, string>(10);

            for (int i = 0; i < 5; i++)
            {
                cache.AddOrUpdate(i, i + "VALUE");
            }

            for (int i = 0; i < 3; i++)
            {
                cache.Remove(i);
            }

            Assert.Equal(2, cache.Count);

            // Check if cache still has the same values that were added.
            for (int i = 3; i < 5; i++)
            {
                cache.TryGetValue(i, out string value);
                Assert.Equal(i + "VALUE", value);
            }
        }

        [Fact]
        public void CacheKeepsMostRecentlyUsedItems()
        {
            var cache = new MemoryCountCache<int, string>(10);

            for (int i = 0; i < 15; i++)
            {
                cache.AddOrUpdate(i, RandomUtils.GetInt32().ToString());

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
            for (int i = 0; i < 15; i++)
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
