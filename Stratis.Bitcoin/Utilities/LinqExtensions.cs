using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for collections.
    /// </summary>
    /// <remarks>TODO: Why is this called LinqExtensions? Wouldn't CollectionsExtensions be better?</remarks>
    public static class LinqExtensions
    {
        /// <summary>
        /// Calculates a median value of a collection of integers.
        /// </summary>
        /// <param name="source">Collection of numbers to count median of.</param>
        /// <returns>Median value, or 0 if the collection is empty.</returns>
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
