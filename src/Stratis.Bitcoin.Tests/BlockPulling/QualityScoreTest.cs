using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.BlockPulling;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling
{
    /// <summary>
    /// Tests of <see cref="QualityScore"/> class.
    /// </summary>
    public class QualityScoreTest
    {
        /// <summary>
        /// Checks that <see cref="QualityScore.AverageBlockTimePerKb"/> is correctly calculated 
        /// if the number of samples is low.
        /// </summary>
        [Fact]
        public void AddSampleWithFewSamplesCorrectlyCalculatesRecentHistoryAverage()
        {
            QualityScore qualityScore = new QualityScore(5, new LoggerFactory());

            int peerCount = 3;
            Mock<IBlockPullerBehavior>[] peers = new Mock<IBlockPullerBehavior>[peerCount];

            for (int i = 0; i < peerCount; i++)
            {
                peers[i] = new Mock<IBlockPullerBehavior>();
                peers[i].Setup(b => b.QualityScore).Returns(QualityScore.MaxScore / 3);
            }

            qualityScore.AddSample(peers[0].Object, 30, 2048); // 15 ms/Kb
            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 15.0) < 0.00001);

            qualityScore.AddSample(peers[1].Object, 50, 1024); // 50 ms/Kb
            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 32.5) < 0.00001);

            qualityScore.AddSample(peers[2].Object, 40, 1024); // 40 ms/Kb
            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 35.0) < 0.00001);

            qualityScore.AddSample(peers[2].Object, 30, 2048); // 15 ms/Kb
            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 30.0) < 0.00001);

            qualityScore.AddSample(peers[2].Object, 30, 3072); // 10 ms/Kb
            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 26.0) < 0.00001);
        }

        /// <summary>
        /// Checks that <see cref="QualityScore.AverageBlockTimePerKb"/> is correctly calculated 
        /// if the number of samples exceeds the capacity of the history.
        /// </summary>
        [Fact]
        public void AddSampleWithMoreSamplesThanHistoryCapacityCorrectlyCalculatesRecentHistoryAverage()
        {
            QualityScore qualityScore = new QualityScore(5, new LoggerFactory());

            int peerCount = 3;
            Mock<IBlockPullerBehavior>[] peers = new Mock<IBlockPullerBehavior>[peerCount];

            for (int i = 0; i < peerCount; i++)
            {
                peers[i] = new Mock<IBlockPullerBehavior>();
                peers[i].Setup(b => b.QualityScore).Returns(QualityScore.MaxScore / 3);
            }

            Random rnd = new Random();
            for (int i = 0; i < 1000; i++)
            {
                int peerIndex = rnd.Next(peerCount);
                int time = rnd.Next(100000);
                int size = rnd.Next(1024 * 1024);
                qualityScore.AddSample(peers[peerIndex].Object, time, size);
            }

            // Only last 5 samples will be remembered.
            qualityScore.AddSample(peers[0].Object, 30, 2048); // 15 ms/Kb
            qualityScore.AddSample(peers[1].Object, 50, 1024); // 50 ms/Kb
            qualityScore.AddSample(peers[2].Object, 40, 1024); // 40 ms/Kb
            qualityScore.AddSample(peers[2].Object, 30, 2048); // 15 ms/Kb
            qualityScore.AddSample(peers[2].Object, 30, 3072); // 10 ms/Kb

            Assert.True(Math.Abs(qualityScore.AverageBlockTimePerKb - 26.0) < 0.00001);
        }

        /// <summary>
        /// Checks that <see cref="QualityScore.CalculateQualityAdjustment"/> is correctly calculated. 
        /// More specifically, we check if the score adjustment is positive (i.e. rewarding) if the block time 
        /// is less then 2x current average and negative (i.e. penalizing) if the block time is more than 2x average.
        /// </summary>
        [Fact]
        public void CalculateQualityAdjustmentCorrectlyCalculates()
        {
            QualityScore qualityScore = new QualityScore(5, new LoggerFactory());

            int peerCount = 3;
            Mock<IBlockPullerBehavior>[] peers = new Mock<IBlockPullerBehavior>[peerCount];

            for (int i = 0; i < peerCount; i++)
            {
                peers[i] = new Mock<IBlockPullerBehavior>();
                peers[i].Setup(b => b.QualityScore).Returns(QualityScore.MaxScore / 3);
            }

            qualityScore.AddSample(peers[0].Object, 30, 2048);
            qualityScore.AddSample(peers[1].Object, 50, 1024);
            qualityScore.AddSample(peers[2].Object, 40, 1024);
            qualityScore.AddSample(peers[2].Object, 30, 2048);
            qualityScore.AddSample(peers[2].Object, 30, 3072);

            // Average of the five samples above is now 26 ms/Kb.
            double avg = 26.0;

            Random rnd = new Random();

            // First we test values that should lead to reward adjustments, these are block times with less than 2x the current average.
            long timePerKb = 1;
            for (; timePerKb < 2 * avg; timePerKb++)
            {
                double coef = rnd.NextDouble() * 10;
                Assert.True(qualityScore.CalculateQualityAdjustment((long)(Math.Floor(coef * timePerKb)), (int)(Math.Ceiling(coef * 1024))) > 0);
            }

            // Then we test values that should lead to penalty adjustment, these are block times with more than 2x the current average.
            for (timePerKb = (long)(2 * avg) + 1; timePerKb < 10 * avg; timePerKb++)
            {
                double coef = rnd.NextDouble() * 10 + 0.1;
                Assert.True(qualityScore.CalculateQualityAdjustment((long)(Math.Ceiling(coef * timePerKb)), (int)(Math.Floor(coef * 1024))) < 0);
            }
        }

        /// <summary>
        /// Checks that <see cref="QualityScore.AverageBlockTimePerKb"/> is correctly evaluated.
        /// </summary>
        [Fact]
        public void IsPenaltyDiscardedEvaluatesCorrectly()
        {
            QualityScore qualityScore = new QualityScore(5, new LoggerFactory());

            int peerCount = 3;
            Mock<IBlockPullerBehavior>[] peers = new Mock<IBlockPullerBehavior>[peerCount];
            double[] peerScores = new double[] { 3.3, 1.0, 1.5 };

            for (int i = 0; i < peerCount; i++)
            {
                peers[i] = new Mock<IBlockPullerBehavior>();
                peers[i].Setup(b => b.QualityScore).Returns(peerScores[i]);
            }

            qualityScore.AddSample(peers[0].Object, 30, 2048); 
            Assert.False(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[1].Object, 50, 1024); 
            Assert.False(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[2].Object, 40, 1024); 
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[0].Object, 30, 2048); 
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[0].Object, 30, 3072); 
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[0].Object, 30, 2048);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[1].Object, 30, 2048);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[1].Object, 50, 1024);
            Assert.False(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[2].Object, 40, 1024);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[2].Object, 40, 1024);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[2].Object, 40, 1024);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[2].Object, 40, 1024);
            Assert.True(qualityScore.IsPenaltyDiscarded());

            qualityScore.AddSample(peers[0].Object, 30, 3072);
            Assert.False(qualityScore.IsPenaltyDiscarded());
        }
    }
}
