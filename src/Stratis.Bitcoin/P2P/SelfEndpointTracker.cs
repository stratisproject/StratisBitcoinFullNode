using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        /// <summary>Bag to hold the endpoints currently known to be itself.</summary>
        private readonly ConcurrentBag<IPEndPoint> knownSelfEndpoints = new ConcurrentBag<IPEndPoint>();

        /// <inheritdoc/>
        public void Add(IPEndPoint ipEndPoint)
        {
            this.knownSelfEndpoints.Add(ipEndPoint);
        }
        /// <inheritdoc/>
        public bool IsSelf(IPEndPoint ipEndPoint)
        {
            return this.knownSelfEndpoints.Contains(ipEndPoint);
        }
    }
}