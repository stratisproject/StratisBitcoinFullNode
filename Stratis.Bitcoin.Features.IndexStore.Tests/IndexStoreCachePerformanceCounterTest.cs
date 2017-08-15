using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Features.IndexStore;
using Xunit;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreCachePerformanceCounterTest
    {
        private IndexStoreCachePerformanceCounter performanceCounter;

        public IndexStoreCachePerformanceCounterTest()
        {
            this.performanceCounter = new IndexStoreCachePerformanceCounter();
        }

        [Fact]
        public void Constructor_InitializesTimeAndCount_IX()
        {
            Assert.Equal(0, this.performanceCounter.CacheHitCount);
            Assert.Equal(0, this.performanceCounter.CacheMissCount);
            Assert.Equal(0, this.performanceCounter.CacheRemoveCount);
            Assert.Equal(0, this.performanceCounter.CacheSetCount);

            Assert.Equal(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [Fact]
        public void AddCacheHitCount_WithGivenAmount_IncrementsHitCount_IX()
        {
            this.performanceCounter.AddCacheHitCount(15);

            Assert.Equal(15, this.performanceCounter.CacheHitCount);
            Assert.Equal(0, this.performanceCounter.CacheMissCount);
            Assert.Equal(0, this.performanceCounter.CacheRemoveCount);
            Assert.Equal(0, this.performanceCounter.CacheSetCount);
        }

        [Fact]
        public void AddCacheMissCount_WithGivenAmount_IncrementsMissCount_IX()
        {
            this.performanceCounter.AddCacheMissCount(15);

            Assert.Equal(0, this.performanceCounter.CacheHitCount);
            Assert.Equal(15, this.performanceCounter.CacheMissCount);
            Assert.Equal(0, this.performanceCounter.CacheRemoveCount);
            Assert.Equal(0, this.performanceCounter.CacheSetCount);
        }

        [Fact]
        public void AddCacheRemoveCount_WithGivenAmount_IncrementsRemoveCount_IX()
        {
            this.performanceCounter.AddCacheRemoveCount(15);

            Assert.Equal(0, this.performanceCounter.CacheHitCount);
            Assert.Equal(0, this.performanceCounter.CacheMissCount);
            Assert.Equal(15, this.performanceCounter.CacheRemoveCount);
            Assert.Equal(0, this.performanceCounter.CacheSetCount);
        }

        [Fact]
        public void AddCacheSetCount_WithGivenAmount_IncrementsSetCount_IX()
        {
            this.performanceCounter.AddCacheSetCount(15);

            Assert.Equal(0, this.performanceCounter.CacheHitCount);
            Assert.Equal(0, this.performanceCounter.CacheMissCount);
            Assert.Equal(0, this.performanceCounter.CacheRemoveCount);
            Assert.Equal(15, this.performanceCounter.CacheSetCount);
        }

        [Fact]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount_IX()
        {
            this.performanceCounter.AddCacheHitCount(15);
            this.performanceCounter.AddCacheMissCount(7);
            this.performanceCounter.AddCacheRemoveCount(3);
            this.performanceCounter.AddCacheSetCount(1);

            var snapshot1 = this.performanceCounter.Snapshot();

            this.performanceCounter.AddCacheHitCount(50);
            this.performanceCounter.AddCacheMissCount(9);
            this.performanceCounter.AddCacheRemoveCount(6);
            this.performanceCounter.AddCacheSetCount(67);

            var snapshot2 = this.performanceCounter.Snapshot();

            Assert.Equal(15, snapshot1.TotalCacheHitCount);
            Assert.Equal(7, snapshot1.TotalCacheMissCount);
            Assert.Equal(3, snapshot1.TotalCacheRemoveCount);
            Assert.Equal(1, snapshot1.TotalCacheSetCount);

            Assert.Equal(65, snapshot2.TotalCacheHitCount);
            Assert.Equal(16, snapshot2.TotalCacheMissCount);
            Assert.Equal(9, snapshot2.TotalCacheRemoveCount);
            Assert.Equal(68, snapshot2.TotalCacheSetCount);
        }        
    }
}