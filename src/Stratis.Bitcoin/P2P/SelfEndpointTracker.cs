using System.Net;
using ConcurrentCollections;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public class SelfEndpointTracker : ISelfEndpointTracker
    {
        /// <summary>Hashset to hold the endpoints currently known to be itself.</summary>
        private readonly ConcurrentHashSet<IPEndPoint> knownSelfEndpoints = new ConcurrentHashSet<IPEndPoint>();

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