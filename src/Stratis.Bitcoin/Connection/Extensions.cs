using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Connection
{
    public static class Extensions
    {
        public static IEnumerable<T> TakeAndRemove<T>(this Queue<T> queue, int count)
        {
            count = Math.Min(queue.Count, count);
            for (int i = 0; i < count; i++)
                yield return queue.Dequeue();
        }
    }
}
