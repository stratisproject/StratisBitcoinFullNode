using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    [TestClass]
    public class BlockStoreCachePerformanceCounterTest
    {
        private BlockStoreCachePerformanceCounter performanceCounter;

        [TestInitialize]
        public void Initialize()
        {
            this.performanceCounter = new BlockStoreCachePerformanceCounter();
        }

        [TestMethod]
        public void Constructor_InitializesTimeAndCount()
        {
            Assert.AreEqual(0, this.performanceCounter.CacheHitCount);
            Assert.AreEqual(0, this.performanceCounter.CacheMissCount);
            Assert.AreEqual(0, this.performanceCounter.CacheRemoveCount);
            Assert.AreEqual(0, this.performanceCounter.CacheSetCount);

            Assert.AreEqual(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [TestMethod]
        public void AddCacheHitCount_WithGivenAmount_IncrementsHitCount()
        {
            this.performanceCounter.AddCacheHitCount(15);

            Assert.AreEqual(15, this.performanceCounter.CacheHitCount);
            Assert.AreEqual(0, this.performanceCounter.CacheMissCount);
            Assert.AreEqual(0, this.performanceCounter.CacheRemoveCount);
            Assert.AreEqual(0, this.performanceCounter.CacheSetCount);
        }

        [TestMethod]
        public void AddCacheMissCount_WithGivenAmount_IncrementsMissCount()
        {
            this.performanceCounter.AddCacheMissCount(15);

            Assert.AreEqual(0, this.performanceCounter.CacheHitCount);
            Assert.AreEqual(15, this.performanceCounter.CacheMissCount);
            Assert.AreEqual(0, this.performanceCounter.CacheRemoveCount);
            Assert.AreEqual(0, this.performanceCounter.CacheSetCount);
        }

        [TestMethod]
        public void AddCacheRemoveCount_WithGivenAmount_IncrementsRemoveCount()
        {
            this.performanceCounter.AddCacheRemoveCount(15);

            Assert.AreEqual(0, this.performanceCounter.CacheHitCount);
            Assert.AreEqual(0, this.performanceCounter.CacheMissCount);
            Assert.AreEqual(15, this.performanceCounter.CacheRemoveCount);
            Assert.AreEqual(0, this.performanceCounter.CacheSetCount);
        }

        [TestMethod]
        public void AddCacheSetCount_WithGivenAmount_IncrementsSetCount()
        {
            this.performanceCounter.AddCacheSetCount(15);

            Assert.AreEqual(0, this.performanceCounter.CacheHitCount);
            Assert.AreEqual(0, this.performanceCounter.CacheMissCount);
            Assert.AreEqual(0, this.performanceCounter.CacheRemoveCount);
            Assert.AreEqual(15, this.performanceCounter.CacheSetCount);
        }

        [TestMethod]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount()
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

            Assert.AreEqual(15, snapshot1.TotalCacheHitCount);
            Assert.AreEqual(7, snapshot1.TotalCacheMissCount);
            Assert.AreEqual(3, snapshot1.TotalCacheRemoveCount);
            Assert.AreEqual(1, snapshot1.TotalCacheSetCount);

            Assert.AreEqual(65, snapshot2.TotalCacheHitCount);
            Assert.AreEqual(16, snapshot2.TotalCacheMissCount);
            Assert.AreEqual(9, snapshot2.TotalCacheRemoveCount);
            Assert.AreEqual(68, snapshot2.TotalCacheSetCount);
        }        
    }
}