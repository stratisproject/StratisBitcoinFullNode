using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    [TestClass]
    public class BlockStoreRepositoryPerformanceSnapshotTest
    { 
        [TestMethod]
        public void Constructor_InitializesCounters()
        {
            var snapshot = new BlockStoreRepositoryPerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = DateTime.UtcNow
            };

            Assert.AreEqual(1301, snapshot.TotalRepositoryHitCount);
            Assert.AreEqual(2352, snapshot.TotalRepositoryMissCount);
            Assert.AreEqual(1244, snapshot.TotalRepositoryDeleteCount);
            Assert.AreEqual(6452, snapshot.TotalRepositoryInsertCount);
        }

        [TestMethod]
        public void SubtractOperator_SubtractsValuesFromMultipleSnapshots_CreatesNewSnapshot()
        {
            var snapshot = new BlockStoreRepositoryPerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 1, 1, 1)
            };

            var snapshot2 = new BlockStoreRepositoryPerformanceSnapshot(4312, 3552, 2216, 9023)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 3, 1, 1)
            };

            BlockStoreRepositoryPerformanceSnapshot snapshot3 = snapshot2 - snapshot;

            Assert.AreEqual(3011, snapshot3.TotalRepositoryHitCount);
            Assert.AreEqual(1200, snapshot3.TotalRepositoryMissCount);
            Assert.AreEqual(972, snapshot3.TotalRepositoryDeleteCount);
            Assert.AreEqual(2571, snapshot3.TotalRepositoryInsertCount);
            Assert.AreEqual(new DateTime(2017, 1, 1, 1, 1, 1), snapshot3.Start);
            Assert.AreEqual(new DateTime(2017, 1, 1, 3, 1, 1), snapshot3.Taken);
        }
    }
}