namespace NBitcoin.Indexer
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    /// <summary>
    /// A retry strategy with back-off parameters for calculating the exponential delay between retries.
    /// </summary>
    internal class ExponentialBackoff
    {
        private readonly int retryCount;
        private readonly TimeSpan minBackoff;
        private readonly TimeSpan maxBackoff;
        private readonly TimeSpan deltaBackoff;


        public ExponentialBackoff(int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
        {
            this.retryCount = retryCount;
            this.minBackoff = minBackoff;
            this.maxBackoff = maxBackoff;
            this.deltaBackoff = deltaBackoff;
        }

        public async Task Do(Action act, TaskScheduler scheduler = null)
        {
            Exception lastException = null;
            int retryCount = -1;

            TimeSpan wait;

            while (true)
            {
                try
                {
                    var task = new Task(act);
                    task.Start(scheduler);
                    await task.ConfigureAwait(false);
                    break;
                }
                catch (OutOfMemoryException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
                retryCount++;
                if (!GetShouldRetry(retryCount, lastException, out wait))
                {
                    ExceptionDispatchInfo.Capture(lastException).Throw();
                }
                else
                {
                    await Task.Delay(wait).ConfigureAwait(false);
                }
            }
        }

        internal bool GetShouldRetry(int currentRetryCount, Exception lastException, out TimeSpan retryInterval)
        {
            if (currentRetryCount < this.retryCount)
            {
                var random = new Random();

                var delta = (int)((Math.Pow(2.0, currentRetryCount) - 1.0) * random.Next((int)(this.deltaBackoff.TotalMilliseconds * 0.8), (int)(this.deltaBackoff.TotalMilliseconds * 1.2)));
                var interval = (int)Math.Min(checked(this.minBackoff.TotalMilliseconds + delta), this.maxBackoff.TotalMilliseconds);
                retryInterval = TimeSpan.FromMilliseconds(interval);

                return true;
            }

            retryInterval = TimeSpan.Zero;
            return false;
        }
    }
}