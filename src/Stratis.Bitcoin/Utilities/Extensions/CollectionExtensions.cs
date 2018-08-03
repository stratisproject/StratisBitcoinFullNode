using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    /// <summary>
    /// Extension methods for collections.
    /// </summary>
    [DebuggerStepThrough]
    public static class CollectionExtensions
    {
        /// <summary>
        /// An extension that will check if an <see cref="IEnumerable{T}"/> is empty.
        /// </summary>
        /// <typeparam name="TSource">The type of enumerable.</typeparam>
        /// <param name="source">The enumerable to check.</param>
        /// <returns><c>true</c> if empty otherwise <c>false</c>.</returns>
        public static bool IsEmpty<TSource>(this IEnumerable<TSource> source)
        {
            return !source.DefaultIfEmpty().Any();
        }

        /// <summary>
        /// An extension that will check if an <see cref="IList{T}"/> is empty.
        /// </summary>
        /// <typeparam name="TSource">The type of enumerable.</typeparam>
        /// <param name="source">The enumerable to check.</param>
        /// <returns><c>true</c> if empty otherwise <c>false</c>.</returns>
        public static bool IsEmpty<TSource>(this IList<TSource> source)
        {
            return (source == null) || (source.Count == 0);
        }

        /// <summary>
        /// An extension that will check if an <see cref="Array"/> is empty.
        /// </summary>
        /// <param name="source">The enumerable to check.</param>
        /// <returns><c>true</c> if empty otherwise <c>false</c>.</returns>
        public static bool IsEmpty(this Array source)
        {
            return (source == null) || (source.Length == 0);
        }
    }
}
