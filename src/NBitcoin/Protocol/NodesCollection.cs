using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace NBitcoin.Protocol
{
    public class NodeEventArgs : EventArgs
    {
        public bool Added { get; private set; }
        public Node Node { get; private set; }

        public NodeEventArgs(Node node, bool added)
        {
            this.Added = added;
            this.Node = node;
        }
    }

    public interface IReadOnlyNodesCollection : IEnumerable<Node>
    {
        event EventHandler<NodeEventArgs> Added;
        event EventHandler<NodeEventArgs> Removed;

        Node FindByEndpoint(IPEndPoint endpoint);
        Node FindByIp(IPAddress ip);
        Node FindLocal();
    }

    public class NodesCollection : IEnumerable<Node>, IReadOnlyNodesCollection
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

        private ConcurrentDictionary<Node, Node> nodes;

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
        public class NodeComparer : IEqualityComparer<Node>
        {
            public bool Equals(Node nodeA, Node nodeB)
            {
                if ((nodeA == null) || (nodeB == null))
                    return (nodeA == null) && (nodeB == null);

                return (nodeA.RemoteSocketAddress.MapToIPv6().ToString() == nodeB.RemoteSocketAddress.MapToIPv6().ToString()) && (nodeA.RemoteSocketPort == nodeB.RemoteSocketPort);
            }

            public int GetHashCode(Node node)
            {
                if (node == null)
                    return 0;

                return node.RemoteSocketPort.GetHashCode() ^ node.RemoteSocketAddress.MapToIPv6().ToString().GetHashCode();
            }
        }

        public NodesCollection()
        {
            this.bridge = new Bridge(this.MessageProducer);
            this.nodes = new ConcurrentDictionary<Node, Node>(new NodeComparer());
        }

        public bool Add(Node node)
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

        public bool Remove(Node node)
        {
            Node old;
            if (this.nodes.TryRemove(node, out old))
            {
                node.MessageProducer.RemoveMessageListener(this.bridge);
                this.OnNodeRemoved(old);
                return true;
            }

            return false;
        }

        private void OnNodeAdded(Node node)
        {
            this.Added?.Invoke(this, new NodeEventArgs(node, true));
        }

        public void OnNodeRemoved(Node node)
        {
            this.Removed?.Invoke(this, new NodeEventArgs(node, false));
        }

        public Node FindLocal()
        {
            return this.FindByIp(IPAddress.Loopback);
        }

        public Node FindByIp(IPAddress ip)
        {
            ip = ip.EnsureIPv6();
            return this.nodes.Where(n => Match(ip, null, n.Key)).Select(s => s.Key).FirstOrDefault();
        }

        public Node FindByEndpoint(IPEndPoint endpoint)
        {
            IPAddress ip = endpoint.Address.EnsureIPv6();
            int port = endpoint.Port;
            return this.nodes.Select(n => n.Key).FirstOrDefault(n => Match(ip, port, n));
        }

        private static bool Match(IPAddress ip, int? port, Node node)
        {
            if (port.HasValue)
            {
                return ((node.State > NodeState.Disconnecting) && node.RemoteSocketAddress.Equals(ip) && (node.RemoteSocketPort == port.Value))
                    || (node.PeerVersion.AddressFrom.Address.Equals(ip) && (node.PeerVersion.AddressFrom.Port == port.Value));
            }
            else
            {
                return ((node.State > NodeState.Disconnecting) && node.RemoteSocketAddress.Equals(ip))
                    || node.PeerVersion.AddressFrom.Address.Equals(ip);
            }
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return this.nodes.Select(n => n.Key).AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void DisconnectAll(CancellationToken cancellation = default(CancellationToken))
        {
            foreach (KeyValuePair<Node, Node> node in this.nodes)
                node.Key.DisconnectAsync();
        }

        public void Clear()
        {
            this.nodes.Clear();
        }
    }
}