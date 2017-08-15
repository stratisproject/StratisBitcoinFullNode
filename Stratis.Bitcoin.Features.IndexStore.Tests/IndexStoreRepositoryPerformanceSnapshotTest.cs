using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.IndexStore;
using Xunit;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreRepositoryPerformanceSnapshotTest
    {
        public IndexStoreRepositoryPerformanceSnapshotTest()
        {
        }

        [Fact]
        public void Constructor_InitializesCounters_IX()
        {
            var snapshot = new IndexStoreRepositoryPerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = DateTime.UtcNow
            };

            Assert.Equal(1301, snapshot.TotalRepositoryHitCount);
            Assert.Equal(2352, snapshot.TotalRepositoryMissCount);
            Assert.Equal(1244, snapshot.TotalRepositoryDeleteCount);
            Assert.Equal(6452, snapshot.TotalRepositoryInsertCount);
        }

        [Fact]
        public void SubtractOperator_SubtractsValuesFromMultipleSnapshots_CreatesNewSnapshot_IX()
        {
            var snapshot = new IndexStoreRepositoryPerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 1, 1, 1)
            };

            var snapshot2 = new IndexStoreRepositoryPerformanceSnapshot(4312, 3552, 2216, 9023)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = new DateTime(2017, 1, 1, 3, 1, 1)
            };

            IndexStoreRepositoryPerformanceSnapshot snapshot3 = snapshot2 - snapshot;

            Assert.Equal(3011, snapshot3.TotalRepositoryHitCount);
            Assert.Equal(1200, snapshot3.TotalRepositoryMissCount);
            Assert.Equal(972, snapshot3.TotalRepositoryDeleteCount);
            Assert.Equal(2571, snapshot3.TotalRepositoryInsertCount);
            Assert.Equal(new DateTime(2017, 1, 1, 1, 1, 1), snapshot3.Start);
            Assert.Equal(new DateTime(2017, 1, 1, 3, 1, 1), snapshot3.Taken);
        }
    }
}