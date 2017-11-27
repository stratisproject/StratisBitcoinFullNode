using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NodeEventArgs : EventArgs
    {
        public bool Added { get; private set; }
        public NetworkPeer Node { get; private set; }

        public NodeEventArgs(NetworkPeer node, bool added)
        {
            this.Added = added;
            this.Node = node;
        }
    }

    public interface IReadOnlyNetworkPeerCollection : IEnumerable<NetworkPeer>
    {
        event EventHandler<NodeEventArgs> Added;
        event EventHandler<NodeEventArgs> Removed;

        NetworkPeer FindByEndpoint(IPEndPoint endpoint);
        NetworkPeer FindByIp(IPAddress ip);
        NetworkPeer FindLocal();
    }

    public class NetworkPeerCollection : IEnumerable<NetworkPeer>, IReadOnlyNetworkPeerCollection
    {
        private class Bridge : IMessageListener<IncomingMessage>
        {
            private MessageProducer<IncomingMessage> prod;
            public Bridge(MessageProducer<IncomingMessage> prod)
            {
                this.prod = prod;
            }

            public void PushMessage(IncomingMessage message)
            {
                this.prod.PushMessage(message);
            }
        }

        private Bridge bridge;

        public MessageProducer<IncomingMessage> MessageProducer { get; private set; }

        private ConcurrentDictionary<NetworkPeer, NetworkPeer> nodes;

        public int Count
        {
            get
            {
                return this.nodes.Count;
            }
        }

        public event EventHandler<NodeEventArgs> Added;
        public event EventHandler<NodeEventArgs> Removed;

        /// <summary>
        /// Provides a comparer to specify how nodes are compared for equality.
        /// </summary>
        public class NodeComparer : IEqualityComparer<NetworkPeer>
        {
            public bool Equals(NetworkPeer nodeA, NetworkPeer nodeB)
            {
                if ((nodeA == null) || (nodeB == null))
                    return (nodeA == null) && (nodeB == null);

                return (nodeA.RemoteSocketAddress.MapToIPv6().ToString() == nodeB.RemoteSocketAddress.MapToIPv6().ToString()) && (nodeA.RemoteSocketPort == nodeB.RemoteSocketPort);
            }

            public int GetHashCode(NetworkPeer node)
            {
                if (node == null)
                    return 0;

                return node.RemoteSocketPort.GetHashCode() ^ node.RemoteSocketAddress.MapToIPv6().ToString().GetHashCode();
            }
        }

        public NetworkPeerCollection()
        {
            this.MessageProducer = new MessageProducer<IncomingMessage>();
            this.bridge = new Bridge(this.MessageProducer);
            this.nodes = new ConcurrentDictionary<NetworkPeer, NetworkPeer>(new NodeComparer());
        }

        public bool Add(NetworkPeer node)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            if (this.nodes.TryAdd(node, node))
            {
                node.MessageProducer.AddMessageListener(this.bridge);
                this.OnNodeAdded(node);
                return true;
            }

            return false;
        }

        public bool Remove(NetworkPeer node)
        {
            NetworkPeer old;
            if (this.nodes.TryRemove(node, out old))
            {
                node.MessageProducer.RemoveMessageListener(this.bridge);
                this.OnNodeRemoved(old);
                return true;
            }

            return false;
        }

        private void OnNodeAdded(NetworkPeer node)
        {
            this.Added?.Invoke(this, new NodeEventArgs(node, true));
        }

        public void OnNodeRemoved(NetworkPeer node)
        {
            this.Removed?.Invoke(this, new NodeEventArgs(node, false));
        }

        public NetworkPeer FindLocal()
        {
            return this.FindByIp(IPAddress.Loopback);
        }

        public NetworkPeer FindByIp(IPAddress ip)
        {
            ip = ip.EnsureIPv6();
            return this.nodes.Where(n => Match(ip, null, n.Key)).Select(s => s.Key).FirstOrDefault();
        }

        public NetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            IPAddress ip = endpoint.Address.EnsureIPv6();
            int port = endpoint.Port;
            return this.nodes.Select(n => n.Key).FirstOrDefault(n => Match(ip, port, n));
        }

        private static bool Match(IPAddress ip, int? port, NetworkPeer node)
        {
            if (port.HasValue)
            {
                return ((node.State > NetworkPeerState.Disconnecting) && node.RemoteSocketAddress.Equals(ip) && (node.RemoteSocketPort == port.Value))
                    || (node.PeerVersion.AddressFrom.Address.Equals(ip) && (node.PeerVersion.AddressFrom.Port == port.Value));
            }
            else
            {
                return ((node.State > NetworkPeerState.Disconnecting) && node.RemoteSocketAddress.Equals(ip))
                    || node.PeerVersion.AddressFrom.Address.Equals(ip);
            }
        }

        public IEnumerator<NetworkPeer> GetEnumerator()
        {
            return this.nodes.Select(n => n.Key).AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void DisconnectAll(CancellationToken cancellation = default(CancellationToken))
        {
            foreach (KeyValuePair<NetworkPeer, NetworkPeer> node in this.nodes)
                node.Key.DisconnectAsync();
        }

        public void Clear()
        {
            this.nodes.Clear();
        }
    }
}