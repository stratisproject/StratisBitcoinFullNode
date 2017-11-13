using System;
using System.Collections.Generic;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    public static class Extensions
    {
        public static T Behavior<T>(this Node node) where T : NodeBehavior
        {
            return node.Behaviors.Find<T>();
        }

        public static IEnumerable<T> TakeAndRemove<T>(this Queue<T> queue, int count)
        {
            count = Math.Min(queue.Count, count);
            for (int i = 0; i < count; i++)
                yield return queue.Dequeue();
        }

        public static string RemoteInfo(this Node node)
        {
            return node.RemoteSocketAddress + ":" + node.RemoteSocketPort;
        }
    }
}
