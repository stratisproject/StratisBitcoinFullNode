using System;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class BackendPerformanceCounterTest
    {
        private BackendPerformanceCounter performanceCounter;

        public BackendPerformanceCounterTest()
        {
            this.performanceCounter = new BackendPerformanceCounter(DateTimeProvider.Default);
        }

        [Fact]
        public void Constructor_InitializesTimeAndCount()
        {
            Assert.Equal(0, this.performanceCounter.InsertedEntities);
            Assert.Equal(0, this.performanceCounter.QueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.InsertTime);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.QueryTime);

            Assert.Equal(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [Fact]
        public void AddInsertedEntities_WithGivenEmount_IncrementsInsertedEntities()
        {
            this.performanceCounter.AddInsertedEntities(15);

            Assert.Equal(15, this.performanceCounter.InsertedEntities);
            Assert.Equal(0, this.performanceCounter.QueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.InsertTime);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.QueryTime);
        }

        [Fact]
        public void AddQueriedEntities_WithGivenEmount_IncrementsQueriedEntities()
        {
            this.performanceCounter.AddQueriedEntities(15);

            Assert.Equal(0, this.performanceCounter.InsertedEntities);
            Assert.Equal(15, this.performanceCounter.QueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.InsertTime);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.QueryTime);
        }

        [Fact]
        public void AddInsertTime_WithGivenEmount_IncrementsInsertTime()
        {
            this.performanceCounter.AddInsertTime(15);

            Assert.Equal(0, this.performanceCounter.InsertedEntities);
            Assert.Equal(0, this.performanceCounter.QueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(15), this.performanceCounter.InsertTime);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.QueryTime);
        }

        [Fact]
        public void AddQueryTime_WithGivenEmount_IncrementsQueryTime()
        {
            this.performanceCounter.AddQueryTime(15);

            Assert.Equal(0, this.performanceCounter.InsertedEntities);
            Assert.Equal(0, this.performanceCounter.QueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(0), this.performanceCounter.InsertTime);
            Assert.Equal(TimeSpan.FromTicks(15), this.performanceCounter.QueryTime);
        }

        [Fact]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount()
        {
            this.performanceCounter.AddInsertedEntities(15);
            this.performanceCounter.AddQueriedEntities(7);
            this.performanceCounter.AddInsertTime(3);
            this.performanceCounter.AddQueryTime(1);

            BackendPerformanceSnapshot snapshot1 = this.performanceCounter.Snapshot();

            this.performanceCounter.AddInsertedEntities(50);
            this.performanceCounter.AddQueriedEntities(9);
            this.performanceCounter.AddInsertTime(6);
            this.performanceCounter.AddQueryTime(67);

            BackendPerformanceSnapshot snapshot2 = this.performanceCounter.Snapshot();

            Assert.Equal(15, snapshot1.TotalInsertedEntities);
            Assert.Equal(7, snapshot1.TotalQueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(3), snapshot1.TotalInsertTime);
            Assert.Equal(TimeSpan.FromTicks(1), snapshot1.TotalQueryTime);

            Assert.Equal(65, snapshot2.TotalInsertedEntities);
            Assert.Equal(16, snapshot2.TotalQueriedEntities);
            Assert.Equal(TimeSpan.FromTicks(9), snapshot2.TotalInsertTime);
            Assert.Equal(TimeSpan.FromTicks(68), snapshot2.TotalQueryTime);
        }
    }
}
