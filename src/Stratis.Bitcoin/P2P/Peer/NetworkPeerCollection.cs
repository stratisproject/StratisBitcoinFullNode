using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerEventArgs : EventArgs
    {
        public bool Added { get; private set; }
        public NetworkPeer peer { get; private set; }

        public NetworkPeerEventArgs(NetworkPeer peer, bool added)
        {
            this.Added = added;
            this.peer = peer;
        }
    }

    public interface IReadOnlyNetworkPeerCollection : IEnumerable<NetworkPeer>
    {
        event EventHandler<NetworkPeerEventArgs> Added;
        event EventHandler<NetworkPeerEventArgs> Removed;

        NetworkPeer FindByEndpoint(IPEndPoint endpoint);
        NetworkPeer FindByIp(IPAddress ip);
        NetworkPeer FindLocal();
    }

    public class NetworkPeerCollection : IEnumerable<NetworkPeer>, IReadOnlyNetworkPeerCollection
    {
        private ConcurrentDictionary<NetworkPeer, NetworkPeer> networkPeers;

        public int Count
        {
            get
            {
                return this.networkPeers.Count;
            }
        }

        public event EventHandler<NetworkPeerEventArgs> Added;
        public event EventHandler<NetworkPeerEventArgs> Removed;

        /// <summary>
        /// Provides a comparer to specify how peers are compared for equality.
        /// </summary>
        public class NetworkPeerComparer : IEqualityComparer<NetworkPeer>
        {
            public bool Equals(NetworkPeer peerA, NetworkPeer peerB)
            {
                if ((peerA == null) || (peerB == null))
                    return (peerA == null) && (peerB == null);

                return (peerA.RemoteSocketAddress.MapToIPv6().ToString() == peerB.RemoteSocketAddress.MapToIPv6().ToString()) && (peerA.RemoteSocketPort == peerB.RemoteSocketPort);
            }

            public int GetHashCode(NetworkPeer peer)
            {
                if (peer == null)
                    return 0;

                return peer.RemoteSocketPort.GetHashCode() ^ peer.RemoteSocketAddress.MapToIPv6().ToString().GetHashCode();
            }
        }

        public NetworkPeerCollection()
        {
            this.networkPeers = new ConcurrentDictionary<NetworkPeer, NetworkPeer>(new NetworkPeerComparer());
        }

        public bool Add(NetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            if (this.networkPeers.TryAdd(peer, peer))
            {
                this.OnPeerAdded(peer);
                return true;
            }

            return false;
        }

        public bool Remove(NetworkPeer peer, string reason)
        {
            NetworkPeer old;
            if (this.networkPeers.TryRemove(peer, out old))
            {
                this.OnPeerRemoved(old);
                peer.Dispose(reason);
                return true;
            }

            return false;
        }

        private void OnPeerAdded(NetworkPeer peer)
        {
            this.Added?.Invoke(this, new NetworkPeerEventArgs(peer, true));
        }

        public void OnPeerRemoved(NetworkPeer peer)
        {
            this.Removed?.Invoke(this, new NetworkPeerEventArgs(peer, false));
        }

        public NetworkPeer FindLocal()
        {
            return this.FindByIp(IPAddress.Loopback);
        }

        public NetworkPeer FindByIp(IPAddress ip)
        {
            ip = ip.EnsureIPv6();
            return this.networkPeers.Where(n => Match(ip, null, n.Key)).Select(s => s.Key).FirstOrDefault();
        }

        public NetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            IPAddress ip = endpoint.Address.EnsureIPv6();
            int port = endpoint.Port;
            return this.networkPeers.Select(n => n.Key).FirstOrDefault(n => Match(ip, port, n));
        }

        private static bool Match(IPAddress ip, int? port, NetworkPeer peer)
        {
            if (port.HasValue)
            {
                return ((peer.State > NetworkPeerState.Disconnecting) && peer.RemoteSocketAddress.Equals(ip) && (peer.RemoteSocketPort == port.Value))
                    || (peer.PeerVersion.AddressFrom.Address.Equals(ip) && (peer.PeerVersion.AddressFrom.Port == port.Value));
            }
            else
            {
                return ((peer.State > NetworkPeerState.Disconnecting) && peer.RemoteSocketAddress.Equals(ip))
                    || peer.PeerVersion.AddressFrom.Address.Equals(ip);
            }
        }

        public IEnumerator<NetworkPeer> GetEnumerator()
        {
            return this.networkPeers.Select(n => n.Key).AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void DisconnectAll(string reason, CancellationToken cancellation = default(CancellationToken))
        {
            foreach (KeyValuePair<NetworkPeer, NetworkPeer> peer in this.networkPeers)
                peer.Key.Dispose(reason);
        }

        public void Clear()
        {
            this.networkPeers.Clear();
        }
    }
}