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
    }
}
