using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods related to the network protocol.
    /// </summary>
    public static class ProtocolExtensions
    {
        /// <summary>
        /// Calculates a median of time offsets of the node's connected peers.
        /// <para>
        /// The peers' time offsets are differences in seconds between the node's clock and the peer's clock.
        /// </para>
        /// </summary>
        /// <param name="source">Collection of connected peer nodes.</param>
        /// <returns>Median time offset among the given nodes.</returns>
        public static long GetMedianTimeOffset(this IEnumerable<INetworkPeer> source)
        {
            return source
                .Where(node => node.TimeOffset.HasValue)
                .Select(node => (long)node.TimeOffset.Value.TotalSeconds)
                .Median();
        }
    }
}
