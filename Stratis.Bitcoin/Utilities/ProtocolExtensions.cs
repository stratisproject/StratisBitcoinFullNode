using System.Collections.Generic;
using System.Linq;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Utilities
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
