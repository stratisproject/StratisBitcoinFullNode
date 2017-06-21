using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    [TestClass]
    public class BlockStoreCachePerformanceSnapshotTest
    {

        [TestMethod]
        public void Constructor_InitializesCounters()
        {
            var snapshot = new BlockStoreCachePerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = DateTime.UtcNow
            };

            Assert.AreEqual(1301, snapshot.TotalCacheHitCount);
            Assert.AreEqual(2352, snapshot.TotalCacheMissCount);
            Assert.AreEqual(1244, snapshot.TotalCacheRemoveCount);
            Assert.AreEqual(6452, snapshot.TotalCacheSetCount);
        }

        [TestMethod]
        public void SubtractOperator_SubtractsValuesFromMultipleSnapshots_CreatesNewSnapshot()
        {
            var snapshot = new BlockStoreCachePerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 1, 1, 1)
            };

            var snapshot2 = new BlockStoreCachePerformanceSnapshot(4312, 3552, 2216, 9023)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 3, 1, 1)
            };

            BlockStoreCachePerformanceSnapshot snapshot3 = snapshot2 - snapshot;

            Assert.AreEqual(3011, snapshot3.TotalCacheHitCount);
            Assert.AreEqual(1200, snapshot3.TotalCacheMissCount);
            Assert.AreEqual(972, snapshot3.TotalCacheRemoveCount);
            Assert.AreEqual(2571, snapshot3.TotalCacheSetCount);
            Assert.AreEqual(new DateTime(2017, 1, 1, 1, 1, 1), snapshot3.Start);
            Assert.AreEqual(new DateTime(2017, 1, 1, 3, 1, 1), snapshot3.Taken);
        }
    }
}