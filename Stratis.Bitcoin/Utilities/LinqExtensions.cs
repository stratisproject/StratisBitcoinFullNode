using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    public static class LinqExtensions
    {
        public static long Median(this IEnumerable<long> source)
        {
            long count = source.LongCount();
            if (count == 0)
                return 0;
            int midpoint = source.Count() / 2;
            IOrderedEnumerable<long> ordered = source.OrderBy(n => n);
            if ((count % 2) == 0)
                return (long)Math.Round(ordered.Skip(midpoint-1).Take(2).Average());
            else
                return ordered.ElementAt(midpoint);
        }
    }
}
