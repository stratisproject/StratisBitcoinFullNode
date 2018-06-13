using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class PeerPerformanceCounter
    {
        private const double MinQualityScore = 0.01;
        private const double SamplelessQualityScore = 0.3;
        private const double MaxQualityScore = 1.0;

        public double QualityScore { get; private set; }
        public int SpeedBytesPerSecond { get; private set; }
        
        private CircularArray<SizeDelaySample> blockSizeDelaySecondsSamples;

        private double averageSizeBytes;
        private double averageDelaySeconds;
        
        public PeerPerformanceCounter()
        {
            this.blockSizeDelaySecondsSamples = new CircularArray<SizeDelaySample>(100);
            this.QualityScore = SamplelessQualityScore;

            this.averageSizeBytes = 0;
            this.averageDelaySeconds = 0;
            this.SpeedBytesPerSecond = 0;
        }

        public void AddSample(long blockSizeBytes, double delaySeconds)
        {
            var newSample = new SizeDelaySample()
            {
                SizeBytes = blockSizeBytes,
                DelaySeconds = delaySeconds
            };

            this.blockSizeDelaySecondsSamples.Add(newSample, out SizeDelaySample unused);

            this.averageSizeBytes = this.RecalculateAverageForSircularArray(this.blockSizeDelaySecondsSamples.Count, this.averageSizeBytes, blockSizeBytes, unused.SizeBytes);
            this.averageDelaySeconds = this.RecalculateAverageForSircularArray(this.blockSizeDelaySecondsSamples.Count, this.averageDelaySeconds, delaySeconds, unused.DelaySeconds);

            this.SpeedBytesPerSecond = (int)(this.averageSizeBytes / this.averageDelaySeconds);
        }

        public void RecalculateQualityScore(int bestSpeedBytesPerSecond)
        {
            this.QualityScore = (double)this.SpeedBytesPerSecond / bestSpeedBytesPerSecond;

            if (this.QualityScore < MinQualityScore)
                this.QualityScore = MinQualityScore;

            if (this.QualityScore > MaxQualityScore)
                this.QualityScore = MaxQualityScore;
        }

        private double RecalculateAverageForSircularArray(int arrayItemsCount, double oldAverage, double newSample, double oldSample)
        {
            // Equality comparison against 0 is ok here
            // https://stackoverflow.com/questions/6598179/the-right-way-to-compare-a-system-double-to-0-a-number-int

            if (oldSample == 0 && oldAverage == 0)
                return newSample;

            if (oldSample == 0)
                return (oldAverage * (arrayItemsCount - 1) + newSample) / arrayItemsCount;

            return (oldAverage * arrayItemsCount - oldSample + newSample) / arrayItemsCount;
        }

        private struct SizeDelaySample
        {
            public long SizeBytes;
            public double DelaySeconds;
        }
    }
}
