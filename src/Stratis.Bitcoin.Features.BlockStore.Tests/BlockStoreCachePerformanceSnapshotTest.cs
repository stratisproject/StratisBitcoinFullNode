﻿using System;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreCachePerformanceSnapshotTest
    {
        public BlockStoreCachePerformanceSnapshotTest()
        {
        }

        [Fact]
        public void Constructor_InitializesCounters()
        {
            var snapshot = new BlockStoreCachePerformanceSnapshot(1301, 2352, 1244, 6452)
            {
                Start = new DateTime(2017, 1, 1),
                Taken = DateTime.UtcNow
            };

            Assert.Equal(1301, snapshot.TotalCacheHitCount);
            Assert.Equal(2352, snapshot.TotalCacheMissCount);
            Assert.Equal(1244, snapshot.TotalCacheRemoveCount);
            Assert.Equal(6452, snapshot.TotalCacheSetCount);
        }

        [Fact]
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

            Assert.Equal(3011, snapshot3.TotalCacheHitCount);
            Assert.Equal(1200, snapshot3.TotalCacheMissCount);
            Assert.Equal(972, snapshot3.TotalCacheRemoveCount);
            Assert.Equal(2571, snapshot3.TotalCacheSetCount);
            Assert.Equal(new DateTime(2017, 1, 1, 1, 1, 1), snapshot3.Start);
            Assert.Equal(new DateTime(2017, 1, 1, 3, 1, 1), snapshot3.Taken);
        }
    }
}
