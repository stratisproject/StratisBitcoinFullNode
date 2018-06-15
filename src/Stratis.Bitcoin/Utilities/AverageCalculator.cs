using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    public class AverageCalculator
    {
        public double Average { get; private set; }

        private CircularArray<double> samples;

        public AverageCalculator(int maxSamples)
        {
            if (maxSamples < 2)
                throw new ArgumentException("Minimal amount of samples is 2.");

            this.Average = 0;
            this.samples = new CircularArray<double>(maxSamples);
        }

        // Expensive operation
        public void SetMaxSamples(int maxSamples)
        {
            if (this.samples.Capacity == maxSamples)
                return;

            List<double> items = this.samples.Reverse().Take(maxSamples).ToList();

            this.samples = new CircularArray<double>(maxSamples);

            foreach (double item in items)
                this.samples.Add(item, out double unused);

            this.Average = items.Average();
        }

        public void AddSample(double sample)
        {
            bool oldSampleExisted = this.samples.Add(sample, out double removedSample);

            if (!oldSampleExisted && this.samples.Count == 1)
            {
                // We have only one sample - it's the average.
                this.Average = sample;
            }
            else if (!oldSampleExisted)
            {
                // Old sample wasn't replaced.
                this.Average = (this.Average * (this.samples.Count - 1) + sample) / this.samples.Count;
            }
            else
            {
                // Old sample was replaced.
                this.Average = (this.Average * this.samples.Count - removedSample + sample) / this.samples.Count;
            }
        }
    }
}
