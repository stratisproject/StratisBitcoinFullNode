using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    [TestClass]
    public class BlockStoreRepositoryPerformanceCounterTest
    {
        private BlockStoreRepositoryPerformanceCounter performanceCounter;

        [TestInitialize]
        public void Initialize()
        {
            this.performanceCounter = new BlockStoreRepositoryPerformanceCounter();
        }

        [TestMethod]
        public void Constructor_InitializesTimeAndCount()
        {
            Assert.AreEqual(0, this.performanceCounter.RepositoryHitCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryMissCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryInsertCount);

            Assert.AreEqual(DateTime.UtcNow.Date, this.performanceCounter.Start.Date);
        }

        [TestMethod]
        public void AddRepositoryHitCount_WithGivenAmount_IncrementsHitCount()
        {
            this.performanceCounter.AddRepositoryHitCount(15);

            Assert.AreEqual(15, this.performanceCounter.RepositoryHitCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryMissCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryInsertCount);
        }

        [TestMethod]
        public void AddRepositoryMissCount_WithGivenAmount_IncrementsMissCount()
        {
            this.performanceCounter.AddRepositoryMissCount(15);

            Assert.AreEqual(0, this.performanceCounter.RepositoryHitCount);
            Assert.AreEqual(15, this.performanceCounter.RepositoryMissCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryInsertCount);
        }

        [TestMethod]
        public void AddRepositoryDeleteCount_WithGivenAmount_IncrementsDeleteCount()
        {
            this.performanceCounter.AddRepositoryDeleteCount(15);

            Assert.AreEqual(0, this.performanceCounter.RepositoryHitCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryMissCount);
            Assert.AreEqual(15, this.performanceCounter.RepositoryDeleteCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryInsertCount);
        }

        [TestMethod]
        public void AddRepositoryInsertCount_WithGivenAmount_IncrementsInsertCount()
        {
            this.performanceCounter.AddRepositoryInsertCount(15);

            Assert.AreEqual(0, this.performanceCounter.RepositoryHitCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryMissCount);
            Assert.AreEqual(0, this.performanceCounter.RepositoryDeleteCount);
            Assert.AreEqual(15, this.performanceCounter.RepositoryInsertCount);
        }

        [TestMethod]
        public void Snapshot_CreatesSnapshotWithCurrentPerformanceCount()
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

            Assert.AreEqual(15, snapshot1.TotalRepositoryHitCount);
            Assert.AreEqual(7, snapshot1.TotalRepositoryMissCount);
            Assert.AreEqual(3, snapshot1.TotalRepositoryDeleteCount);
            Assert.AreEqual(1, snapshot1.TotalRepositoryInsertCount);

            Assert.AreEqual(65, snapshot2.TotalRepositoryHitCount);
            Assert.AreEqual(16, snapshot2.TotalRepositoryMissCount);
            Assert.AreEqual(9, snapshot2.TotalRepositoryDeleteCount);
            Assert.AreEqual(68, snapshot2.TotalRepositoryInsertCount);
        }        
    }
}