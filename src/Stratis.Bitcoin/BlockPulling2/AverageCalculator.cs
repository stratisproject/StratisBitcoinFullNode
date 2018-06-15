using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
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

        public void SetMaxSamples(int maxSamples)
        {
            if (this.samples.Capacity == maxSamples)
                return;

            //TODO resize
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
