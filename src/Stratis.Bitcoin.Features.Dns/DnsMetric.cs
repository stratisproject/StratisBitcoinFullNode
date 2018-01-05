using System.Threading;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Stores metric values for the <see cref="DnsSeedServer"/> object.
    /// </summary>
    public class DnsMetric
    {
        /// <summary>
        /// Defines the DNS request count.
        /// </summary>
        private long requestCount;

        /// <summary>
        /// Defines the maximum count of peers seen since the server started.
        /// </summary>
        private long maxPeerCount;

        /// <summary>
        /// Defines the server failure count.
        /// </summary>
        private int serverFailureCount;

        /// <summary>
        /// Defines the request failure count.
        /// </summary>
        private int requestFailureCount;

        /// <summary>
        /// Defines the current snapshot.
        /// </summary>
        private DnsMetricSnapshot snapshot = new DnsMetricSnapshot();

        /// <summary>
        /// Gets the total number of DNS requests issued to the DNS server since the start of the application.
        /// </summary>
        public long DnsRequestCountSinceStart { get { return this.requestCount; } }

        /// <summary>
        /// Gets the maximum count of peers seen since the start of the application.
        /// </summary>
        public long MaxPeerCountSinceStart { get { return this.maxPeerCount; } }

        /// <summary>
        /// Gets the number of failures of the server since the start of the application (server failures are restarted).
        /// </summary>
        public int DnsServerFailureCountSinceStart { get { return this.serverFailureCount; } }

        /// <summary>
        /// Gets the number of failures processing DNS requests since the start of the application.
        /// </summary>
        public int DnsRequestFailureCountSinceStart { get { return this.requestFailureCount; } }

        /// <summary>
        /// Gets the current snapshot.
        /// </summary>
        public DnsMetricSnapshot CurrentSnapshot { get { return this.snapshot; } }

        /// <summary>
        /// Captures the metrics for the last request.
        /// </summary>
        /// <param name="peerCount">The last measured count of peers available.</param>
        /// <param name="elapsedTicks">The last measured elapsed ticks taken to process the request.</param>
        /// <param name="requestFailed">True if the request failed, otherwise False.</param>
        public void CaptureRequestMetrics(int peerCount, long elapsedTicks, bool requestFailed)
        {
            // Utmost accuracy isn't important, but useful to do this with as few instructions as possible.
            Interlocked.Increment(ref this.requestCount);
            
            if (peerCount > this.maxPeerCount)
            {
                this.maxPeerCount = peerCount;
            }

            if (requestFailed)
            {
                Interlocked.Increment(ref this.requestFailureCount);
            }

            // Capture at snapshot level.
            this.snapshot.CaptureRequestMetrics(peerCount, elapsedTicks, requestFailed);
        }

        /// <summary>
        /// Increments the server failed count.
        /// </summary>
        public void CaptureServerFailedMetric()
        {
            Interlocked.Increment(ref this.serverFailureCount);
            this.snapshot.CaptureServerFailedMetric();
        }

        /// <summary>
        /// Resets the current snapshot and returns the previous one as the result.
        /// </summary>
        /// <returns>Returns the previous snapshot.</returns>
        public DnsMetricSnapshot ResetSnapshot()
        {
            DnsMetricSnapshot previousSnapshot = this.snapshot;
            Interlocked.Exchange(ref this.snapshot, new DnsMetricSnapshot());
            return previousSnapshot;
        }
    }
}
