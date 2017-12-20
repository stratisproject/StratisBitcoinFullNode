using System;
using System.Collections.Generic;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.Connection
{
    public static class Extensions
    {
        public static T Behavior<T>(this NetworkPeer node) where T : NetworkPeerBehavior
        {
            return node.Behaviors.Find<T>();
        }

        public static IEnumerable<T> TakeAndRemove<T>(this Queue<T> queue, int count)
        {
            count = Math.Min(queue.Count, count);
            for (int i = 0; i < count; i++)
                yield return queue.Dequeue();
        }

        public static string RemoteInfo(this NetworkPeer node)
        {
            return node.RemoteSocketAddress + ":" + node.RemoteSocketPort;
        }
    }
}
