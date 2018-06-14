using System;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CachePerformanceCounterTest
    {
        private CachePerformanceCounter performanceCounter;

        public CachePerformanceCounterTest()
        {
            this.performanceCounter = new CachePerformanceCounter(DateTimeProvider.Default);
        }

        [Fact]
        public void Constructor_InitializesTimeAndCount()
        {
            Assert.Equal(0, this.performanceCounter.HitCount);
            Assert.Equal(0, this.performanceCounter.MissCount);

            Assert.Equal(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [Fact]
        public void AddHitCount_WithGivenEmount_IncrementsHitCount()
        {
            this.performanceCounter.AddHitCount(15);

            Assert.Equal(15, this.performanceCounter.HitCount);
            Assert.Equal(0, this.performanceCounter.MissCount);
        }

        [Fact]
        public void AddMissCount_WithGivenEmount_IncrementsMissCount()
        {
            this.performanceCounter.AddMissCount(15);

            Assert.Equal(0, this.performanceCounter.HitCount);
            Assert.Equal(15, this.performanceCounter.MissCount);
        }

        [Fact]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount()
        {
            this.performanceCounter.AddHitCount(15);
            this.performanceCounter.AddMissCount(7);

            CachePerformanceSnapshot snapshot1 = this.performanceCounter.Snapshot();

            this.performanceCounter.AddHitCount(50);
            this.performanceCounter.AddMissCount(9);

            CachePerformanceSnapshot snapshot2 = this.performanceCounter.Snapshot();

            Assert.Equal(15, snapshot1.TotalHitCount);
            Assert.Equal(7, snapshot1.TotalMissCount);

            Assert.Equal(65, snapshot2.TotalHitCount);
            Assert.Equal(16, snapshot2.TotalMissCount);
        }
    }
}
