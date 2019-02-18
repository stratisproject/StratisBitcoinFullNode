using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    public static class ParallelAsync
    {
        /// <summary>
        /// Executes a foreach operation on an IEnumerable in which iterations run asynchronously.
        /// </summary>
        /// <typeparam name="TSource">Item type.</typeparam>
        /// <param name="collection">Enumerated collection.</param>
        /// <param name="maxDegreeOfParallelism">The maximum amount of items that can be processed simultaneously.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="action">Action that is used for processing each item in the collection.</param>
        public static async Task ForEachAsync<TSource>(this IEnumerable<TSource> collection, int maxDegreeOfParallelism, CancellationToken cancellationToken, Func<TSource, CancellationToken, Task> action)
        {
            Guard.Assert(maxDegreeOfParallelism > 0);

            var unfinished = new List<Task>();

            foreach (TSource item in collection)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (unfinished.Count >= maxDegreeOfParallelism)
                {
                    await Task.WhenAny(unfinished).ConfigureAwait(false);

                    foreach (Task toRemove in unfinished.Where(x => x.IsCompleted).ToList())
                        unfinished.Remove(toRemove);
                }

                if (!cancellationToken.IsCancellationRequested)
                    unfinished.Add(action(item, cancellationToken));
            }

            await Task.WhenAll(unfinished).ConfigureAwait(false);
        }
    }
}
