using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
{
    public static class ProtocolExtensions
    {
        public static long GetMedianTimeOffset(this IEnumerable<Node> source)
        {
            return source
                .Where(node => node.TimeOffset.HasValue)
                .Select(node => (long)node.TimeOffset.Value.TotalSeconds)
                .Median();
        }

        public static string GetDefaultConfigurationFilename(this Network network)
        {
            if (network.Equals(Network.StratisMain))
                return "stratis.conf";
            return "bitcoin.conf";
        }
    }
}
