using System;

namespace Stratis.Bitcoin.Utilities
{
    public class RetryOptions
    {
        public RetryOptions()
        {
            this.RetryCount = 1;
            this.Delay = TimeSpan.FromMilliseconds(100);
        }

        public RetryOptions(short retryCount, TimeSpan delay)
        {
            if (retryCount < 1)
                throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count cannot be less or equal to 0");

            this.RetryCount = retryCount;
            this.Delay = delay;
        }

        public static RetryOptions Default => new RetryOptions();

        public short RetryCount { get; }

        public TimeSpan Delay { get; }

        public override string ToString()
        {
            return $"Retry count: {this.RetryCount}; Delay: {this.Delay}";
        }
    }
}