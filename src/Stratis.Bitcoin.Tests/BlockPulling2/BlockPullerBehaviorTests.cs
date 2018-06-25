using System;
using Moq;
using Stratis.Bitcoin.BlockPulling2;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling2
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

            this.behavior = new BlockPullerBehavior(puller.Object, ibdState.Object, loggerFactory);
        }

        [Fact]
        public void InitializesWithDefaultValues()
        {
            Assert.Equal(BlockPullerBehavior.SamplelessQualityScore, this.behavior.QualityScore);
            Assert.Equal(0, this.behavior.SpeedBytesPerSecond);
            Assert.Null(this.behavior.Tip);
        }

        [Fact]
        public void QualityScoreCanGoToMinimum()
        {
            // Add a lot of bad samples to push quality score down. After that peer will have only bad samples.
            this.behavior.Penalize(10, 10);

            this.behavior.RecalculateQualityScore(1000);

            Assert.Equal(BlockPullerBehavior.MinQualityScore, this.behavior.QualityScore);
        }

        [Fact]
        public void QualityScoreCanGoToMaximum()
        {
            this.behavior.AddSample(1000, 1);
            this.behavior.RecalculateQualityScore(1);

            Assert.Equal(BlockPullerBehavior.MaxQualityScore, this.behavior.QualityScore);
        }

        [Fact]
        public void QualityScoreIsRelativeToBestSpeed()
        {
            this.behavior.AddSample(1000, 1);
            this.behavior.RecalculateQualityScore(this.behavior.SpeedBytesPerSecond * 2);

            Assert.True(this.DoubleEqual(0.5, this.behavior.QualityScore));
        }

        [Fact]
        public void SpeedBytesPerSecondCalculatedCorrectly()
        {
            this.behavior.AddSample(1000, 1);
            this.behavior.AddSample(2000, 1);
            this.behavior.AddSample(1000, 10);
            this.behavior.AddSample(500, 2);

            // (500 + 1000 + 2000 + 1000) / (1 + 1 + 10 + 2) == 321.428
            Assert.Equal(321, this.behavior.SpeedBytesPerSecond);
        }

        [Fact]
        public void WhenIBDStateChangesMaxSamplesNumberIsRecalculated()
        {
            Assert.Equal(BlockPullerBehavior.IbdSamplesCount, this.behavior.averageDelaySeconds.GetMaxSamples());
            Assert.Equal(BlockPullerBehavior.IbdSamplesCount, this.behavior.averageSizeBytes.GetMaxSamples());

            this.behavior.OnIbdStateChanged(false);

            Assert.Equal(BlockPullerBehavior.NormalSamplesCount, this.behavior.averageDelaySeconds.GetMaxSamples());
            Assert.Equal(BlockPullerBehavior.NormalSamplesCount, this.behavior.averageSizeBytes.GetMaxSamples());
        }

        [Fact]
        public void CantPenalizeMoreThanParticularPercentage()
        {
            int totalSamples = this.behavior.averageDelaySeconds.GetMaxSamples();

            for (int i = 0; i < totalSamples; i++)
                this.behavior.AddSample(1000, 1);

            Assert.Equal(1000, this.behavior.SpeedBytesPerSecond);

            int maxSamplesToPenalize = (int)(BlockPullerBehavior.MaxSamplesPercentageToPenalize * 100);

            // Try to penalize more than we can.
            this.behavior.Penalize(10, maxSamplesToPenalize * 1000);

            // Make sure we didn't replace all samples with zeros while penalizing.
            Assert.True(this.behavior.SpeedBytesPerSecond > 0);

            // Penalize several times to eventually replace samples with zeros.
            for (int i = 0; i < 100 / maxSamplesToPenalize + 1; i++)
                this.behavior.Penalize(10, maxSamplesToPenalize * 2);

            Assert.Equal(0, this.behavior.SpeedBytesPerSecond);
        }

        private bool DoubleEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }
    }
}
