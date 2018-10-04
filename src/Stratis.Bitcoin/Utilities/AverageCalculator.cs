using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>Calculates average value of last N added samples every time new sample is added.</summary>
    /// <remarks>
    /// Implementation doesn't iterate through the whole collection of samples when average value is being calculated which makes this component more optimal
    /// in terms of performance when frequent calculation of an average value on a set of items is required.
    /// </remarks>
    public class AverageCalculator
    {
        /// <summary>Average value of supplied samples.</summary>
        public double Average { get; private set; }

        /// <summary>Samples used in calculation of the average value.</summary>
        private CircularArray<double> samples;

        /// <summary>Initializes a new instance of the <see cref="AverageCalculator"/> class.</summary>
        /// <param name="maxSamples">Maximum amount of samples that can be used in the calculation of the average value.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="maxSamples"/> is less than 2.</exception>
        public AverageCalculator(int maxSamples)
        {
            if (maxSamples < 2)
                throw new ArgumentException("Minimal amount of samples is 2.");

            this.Average = 0;
            this.samples = new CircularArray<double>(maxSamples);
        }

        /// <summary>Gets the maximum amount of samples that can be used in the calculation of the average value.</summary>
        public int GetMaxSamples()
        {
            return this.samples.Capacity;
        }

        /// <summary>Sets the maximum amount of samples that can be used in the calculation of the average value.</summary>
        /// <remarks>This is an expensive operation since it will require recreating an array of samples.</remarks>
        public void SetMaxSamples(int maxSamples)
        {
            if (this.samples.Capacity == maxSamples)
                return;

            var resized = new CircularArray<double>(maxSamples);

            int skip = 0;
            if (maxSamples < this.samples.Count)
                skip = this.samples.Count - maxSamples;

            foreach (double item in this.samples.Skip(skip))
                resized.Add(item, out double unused);

            this.samples = resized;
            this.Average = this.samples.Count > 0 ? this.samples.Average() : 0;
        }

        /// <summary>Adds a new sample and recalculates <see cref="Average"/> value.</summary>
        /// <param name="sample">New sample.</param>
        public void AddSample(double sample)
        {
            bool oldSampleExisted = this.samples.Add(sample, out double removedSample);

            if (!oldSampleExisted)
            {
                // Old sample wasn't replaced.
                this.Average = (this.Average * (this.samples.Count - 1) + sample) / this.samples.Count;
            }
            else
            {
                // Old sample was replaced.
                this.Average = this.Average + (sample - removedSample) / this.samples.Count;
            }
        }
    }
}
