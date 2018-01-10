using System.Threading;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Stores snapshot metric values for the <see cref="DnsSeedServer"/> object.
    /// </summary>
    public class DnsMetricSnapshot
    {
        /// <summary>
        /// Defines the DNS request count.
        /// </summary>
        private long requestCount;

        /// <summary>
        /// Defines the count of elapsed ticks for this period.
        /// </summary>
        private long elapsedTicks;

        /// <summary>
        /// Defines the count of peers.
        /// </summary>
        private long peerCount;

        /// <summary>
        /// Defines the last measured elapsed ticks when processing DNS request.
        /// </summary>
        private long lastElapsedTicks;

        /// <summary>
        /// Defines the last count of peers.
        /// </summary>
        private int lastPeerCount;

        /// <summary>
        /// Defines the server failure count.
        /// </summary>
        private int serverFailureCount;

        /// <summary>
        /// Defines the request failure count.
        /// </summary>
        private int requestFailureCount;

        /// <summary>
        /// Gets the number of DNS requests issued to the DNS server since the last metrics period.
        /// </summary>
        public long DnsRequestCountSinceLastPeriod { get { return this.requestCount; } }

        /// <summary>
        /// Gets the total number of elasped ticks processing DNS requests since the last metrics period.
        /// </summary>
        public long DnsRequestElapsedTicksSinceLastPeriod { get { return this.elapsedTicks; } }

        /// <summary>
        /// Gets the total number of peers available to DNS responses since the last metrics period.
        /// </summary>
        public long PeerCountSinceLastPeriod { get { return this.peerCount; } }

        /// <summary>
        /// Gets the number of elapsed ticks processing the most recent DNS request.
        /// </summary>
        public long LastDnsRequestElapsedTicks { get { return this.lastElapsedTicks; } }

        /// <summary>
        /// Gets the last number of peers available.
        /// </summary>
        public int LastPeerCount { get { return this.lastPeerCount; } }

        /// <summary>
        /// Gets the number of failures of the server since the last metrics period (server failures are restarted).
        /// </summary>
        public int DnsServerFailureCountSinceLastPeriod { get { return this.serverFailureCount; } }

        /// <summary>
        /// Gets the number of failures processing DNS requests since the last metrics period.
        /// </summary>
        public int DnsRequestFailureCountSinceLastPeriod { get { return this.requestFailureCount; } }

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
            Interlocked.Add(ref this.peerCount, peerCount);
            Interlocked.Add(ref this.elapsedTicks, elapsedTicks);
            Interlocked.Exchange(ref this.lastPeerCount, peerCount);
            Interlocked.Exchange(ref this.lastElapsedTicks, elapsedTicks);
            
            if (requestFailed)
            {
                Interlocked.Increment(ref this.requestFailureCount);
            }
        }

        /// <summary>
        /// Increments the server failed count.
        /// </summary>
        public void CaptureServerFailedMetric()
        {
            Interlocked.Increment(ref this.serverFailureCount);
        }
    }
}
