using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Stratis.Bitcoin.Features.IndexStore;
using Xunit;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreRepositoryPerformanceCounterTest
    {
        private IndexStoreRepositoryPerformanceCounter performanceCounter;

        public IndexStoreRepositoryPerformanceCounterTest()
        {
            this.performanceCounter = new IndexStoreRepositoryPerformanceCounter();
        }

        [Fact]
        public void Constructor_InitializesTimeAndCount_IX()
        {
            Assert.Equal(0, this.performanceCounter.RepositoryHitCount);
            Assert.Equal(0, this.performanceCounter.RepositoryMissCount);
            Assert.Equal(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.Equal(0, this.performanceCounter.RepositoryInsertCount);

            Assert.Equal(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [Fact]
        public void AddRepositoryHitCount_WithGivenAmount_IncrementsHitCount_IX()
        {
            this.performanceCounter.AddRepositoryHitCount(15);

            Assert.Equal(15, this.performanceCounter.RepositoryHitCount);
            Assert.Equal(0, this.performanceCounter.RepositoryMissCount);
            Assert.Equal(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.Equal(0, this.performanceCounter.RepositoryInsertCount);
        }

        [Fact]
        public void AddRepositoryMissCount_WithGivenAmount_IncrementsMissCount_IX()
        {
            this.performanceCounter.AddRepositoryMissCount(15);

            Assert.Equal(0, this.performanceCounter.RepositoryHitCount);
            Assert.Equal(15, this.performanceCounter.RepositoryMissCount);
            Assert.Equal(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.Equal(0, this.performanceCounter.RepositoryInsertCount);
        }

        [Fact]
        public void AddRepositoryDeleteCount_WithGivenAmount_IncrementsDeleteCount_IX()
        {
            this.performanceCounter.AddRepositoryDeleteCount(15);

            Assert.Equal(0, this.performanceCounter.RepositoryHitCount);
            Assert.Equal(0, this.performanceCounter.RepositoryMissCount);
            Assert.Equal(15, this.performanceCounter.RepositoryDeleteCount);
            Assert.Equal(0, this.performanceCounter.RepositoryInsertCount);
        }

        [Fact]
        public void AddRepositoryInsertCount_WithGivenAmount_IncrementsInsertCount_IX()
        {
            this.performanceCounter.AddRepositoryInsertCount(15);

            Assert.Equal(0, this.performanceCounter.RepositoryHitCount);
            Assert.Equal(0, this.performanceCounter.RepositoryMissCount);
            Assert.Equal(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.Equal(15, this.performanceCounter.RepositoryInsertCount);
        }

        [Fact]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount_IX()
        {
            this.performanceCounter.AddRepositoryHitCount(15);
            this.performanceCounter.AddRepositoryMissCount(7);
            this.performanceCounter.AddRepositoryDeleteCount(3);
            this.performanceCounter.AddRepositoryInsertCount(1);

            var snapshot1 = this.performanceCounter.Snapshot();

            this.performanceCounter.AddRepositoryHitCount(50);
            this.performanceCounter.AddRepositoryMissCount(9);
            this.performanceCounter.AddRepositoryDeleteCount(6);
            this.performanceCounter.AddRepositoryInsertCount(67);

            var snapshot2 = this.performanceCounter.Snapshot();

            Assert.Equal(15, snapshot1.TotalRepositoryHitCount);
            Assert.Equal(7, snapshot1.TotalRepositoryMissCount);
            Assert.Equal(3, snapshot1.TotalRepositoryDeleteCount);
            Assert.Equal(1, snapshot1.TotalRepositoryInsertCount);

            Assert.Equal(65, snapshot2.TotalRepositoryHitCount);
            Assert.Equal(16, snapshot2.TotalRepositoryMissCount);
            Assert.Equal(9, snapshot2.TotalRepositoryDeleteCount);
            Assert.Equal(68, snapshot2.TotalRepositoryInsertCount);
        }        
    }
}