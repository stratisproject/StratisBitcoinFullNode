using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AverageCalculatorTests
    {
        private readonly int[] samples;

        public AverageCalculatorTests()
        {
            this.samples = new[] { 10, 20, 30, 40, 50, 60, 5, 10, 15, 20, 25, 30 };
        }

        [Fact]
        public void CanCalculateAverageWhenCapacityIsNotExceeded()
        {
            var calculator = new AverageCalculator(this.samples.Length);

            foreach (int sample in this.samples)
                calculator.AddSample(sample);

            Assert.True(this.DoubleEqual(this.samples.Average(), calculator.Average));
        }

        [Fact]
        public void CanCalculateAverageAfterCapacityExceeded()
        {
            var calculator = new AverageCalculator(3);

            foreach (int sample in this.samples)
                calculator.AddSample(sample);

            Assert.True(this.DoubleEqual(25, calculator.Average));
        }

        [Fact]
        public void CanResizeCapacityWithoutRemovingSamples()
        {
            // Initialize with limit of 200.
            var calculator = new AverageCalculator(200);
            Assert.Equal(200, calculator.GetMaxSamples());

            foreach (int sample in this.samples)
                calculator.AddSample(sample);

            calculator.SetMaxSamples(this.samples.Length);

            Assert.Equal(this.samples.Length, calculator.GetMaxSamples());
            Assert.True(this.DoubleEqual(this.samples.Average(), calculator.Average));
        }

        [Fact]
        public void ResizeToSmallerSamplesCountKeepsTheRightSamples()
        {
            // Initialize with limit of 200.
            var calculator = new AverageCalculator(200);
            Assert.Equal(200, calculator.GetMaxSamples());
            
            // Add 10,20,30
            for (int i = 0; i < 3; i++)
                calculator.AddSample(this.samples[i]);

            // After that only 20,30 should be there
            calculator.SetMaxSamples(2);

            calculator.AddSample(40);

            // There are 2 samples: 30 and 40

            Assert.Equal(2, calculator.GetMaxSamples());
            Assert.True(this.DoubleEqual(35, calculator.Average));
        }

        [Fact]
        public void OrderAfterResizeIsPreserved()
        {
            var calculator = new AverageCalculator(this.samples.Length + 10);

            foreach (int sample in this.samples)
                calculator.AddSample(sample);

            List<int> lastFive = this.samples.Skip(this.samples.Length - 5).ToList(); 
            
            calculator.SetMaxSamples(5);
            
            Assert.Equal(5, calculator.GetMaxSamples());
            Assert.True(this.DoubleEqual(lastFive.Average(), calculator.Average));
        }

        [Fact]
        public void CanResizeCapacityWithRemovingSamples()
        {
            // Initialize with limit of 200.
            var calculator = new AverageCalculator(200);

            foreach (int sample in this.samples)
                calculator.AddSample(sample);

            // After that only last 5 samples should be used.
            calculator.SetMaxSamples(5);

            Assert.Equal(5, calculator.GetMaxSamples());
            Assert.True(this.DoubleEqual(this.samples.Reverse().Take(5).Average(), calculator.Average));
        }

        private bool DoubleEqual(double a, double b)
        {
            return Math.Abs(a - b) < 0.00001;
        }
    }
}
