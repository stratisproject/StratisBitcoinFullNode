using System.Net;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Tracker for endpoints known to be self.
    /// </summary>
    public interface ISelfEndpointTracker
    {
        /// <summary>Adds an endpoint to the currently known list.</summary>
        /// <param name="ipEndPoint">The endpoint to add.</param>
        void Add(IPEndPoint ipEndPoint);

        /// <summary>Checks if endpoint is known to be itself against the pruned dictionary.</summary>
        /// <param name="ipEndPoint">The endpoint to check.</param>
        /// <returns><c>true</c> if self, <c>false</c> if unknown.</returns>
        bool IsSelf(IPEndPoint ipEndPoint);
    }
}