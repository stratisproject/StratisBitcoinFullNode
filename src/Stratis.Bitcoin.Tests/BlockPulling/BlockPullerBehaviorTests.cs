using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling
{
    public class BlockPullerBehaviorTests
    {
        private readonly BlockPullerBehavior behavior;

        public BlockPullerBehaviorTests()
        {
            var puller = new Mock<IBlockPuller>();

            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => true);

            var loggerFactory = new ExtendedLoggerFactory();
            loggerFactory.AddConsoleWithFilters();

            this.behavior = new BlockPullerBehavior(puller.Object, ibdState.Object, DateTimeProvider.Default, loggerFactory);
        }

        [Fact]
        public void InitializesWithDefaultValues()
        {
            Assert.Equal(BlockPullerBehavior.SamplelessQualityScore, this.behavior.QualityScore);
            Assert.Null(this.behavior.Tip);
        }

        [Fact]
        public void QualityScoreCanGoToMinimum()
        {
            // Add a lot of bad samples to push quality score down. After that peer will have only bad samples.
            for (int i = 0; i < 10; i++)
                this.behavior.AddSample(1, 10);

            this.behavior.RecalculateQualityScore(100000);

            Assert.Equal(BlockPullerBehavior.MinQualityScore, this.behavior.QualityScore);
        }

        [Fact]
        public void QualityScoreCanGoToMaximum()
        {
            this.behavior.AddSample(100, 1);
            this.behavior.RecalculateQualityScore(100);

            Assert.Equal(BlockPullerBehavior.MaxQualityScore, this.behavior.QualityScore);
        }

        [Fact]
        public void QualityScoreIsRelativeToBestSpeed()
        {
            this.behavior.AddSample(100, 1);
            this.behavior.RecalculateQualityScore(100 * 2);

            Assert.True(this.DoubleEqual(0.5, this.behavior.QualityScore));
        }

        [Fact]
        public void SpeedCalculatedCorrectlyWhenSeveralBehaviorsStall()
        {
            var behaviors = new List<BlockPullerBehavior>();

            for (int i = 0; i < 125; i++)
            {
                var puller = new Mock<IBlockPuller>();

                var ibdState = new Mock<IInitialBlockDownloadState>();
                ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => true);

                var loggerFactory = new ExtendedLoggerFactory();
                loggerFactory.AddConsoleWithFilters();

                behaviors.Add(new BlockPullerBehavior(puller.Object, ibdState.Object, DateTimeProvider.Default, loggerFactory));
            }

            foreach (BlockPullerBehavior behavior in behaviors)
                behavior.AddSample(0, 30);

            long sum = behaviors.Sum(x => x.SpeedBytesPerSecond);

            Assert.Equal(0, sum);
        }

        private bool DoubleEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }
    }
}
