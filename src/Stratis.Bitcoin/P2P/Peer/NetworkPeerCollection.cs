using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerEventArgs : EventArgs
    {
        public bool Added { get; private set; }

        public INetworkPeer Peer { get; private set; }

        public NetworkPeerEventArgs(INetworkPeer peer, bool added)
        {
            this.Added = added;
            this.Peer = peer;
        }
    }

    public interface IReadOnlyNetworkPeerCollection : IEnumerable<INetworkPeer>
    {
        INetworkPeer FindByEndpoint(IPEndPoint endpoint);

        /// <summary>
        /// Returns all connected peers from a given IP address (the port is irrelevant).
        /// </summary>
        /// <param name="ip">The IP address to filter on.</param>
        /// <returns>The set of connected peers that matched the given IP address.</returns>
        List<INetworkPeer> FindByIp(IPAddress ip);

        INetworkPeer FindLocal();
    }

    public class NetworkPeerCollection : IEnumerable<INetworkPeer>, IReadOnlyNetworkPeerCollection
    {
        private readonly ConcurrentHashSet<INetworkPeer> networkPeers;

        public int Count
        {
            get
            {
                return this.networkPeers.Count;
            }
        }

        /// <summary>
        /// Provides a comparer to specify how peers are compared for equality.
        /// </summary>
        public class NetworkPeerComparer : IEqualityComparer<INetworkPeer>
        {
            public bool Equals(INetworkPeer peerA, INetworkPeer peerB)
            {
                if ((peerA == null) || (peerB == null))
                    return (peerA == null) && (peerB == null);

                return (peerA.RemoteSocketAddress.MapToIPv6().ToString() == peerB.RemoteSocketAddress.MapToIPv6().ToString()) && (peerA.RemoteSocketPort == peerB.RemoteSocketPort);
            }

            public int GetHashCode(INetworkPeer peer)
            {
                if (peer == null)
                    return 0;

                return peer.RemoteSocketPort.GetHashCode() ^ peer.RemoteSocketAddress.MapToIPv6().ToString().GetHashCode();
            }
        }

        public NetworkPeerCollection()
        {
            this.networkPeers = new ConcurrentHashSet<INetworkPeer>(new NetworkPeerComparer());
        }

        public void Add(INetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            this.networkPeers.Add(peer);
        }

        public void Remove(INetworkPeer peer)
        {
            this.networkPeers.TryRemove(peer);
        }

        public INetworkPeer FindLocal()
        {
            return this.FindByIp(IPAddress.Loopback).FirstOrDefault();
        }

        public INetworkPeer FindById(int peerId)
        {
            return this.networkPeers.FirstOrDefault(n => n.Connection.Id == peerId);
        }

        public List<INetworkPeer> FindByIp(IPAddress ip)
        {
            ip = ip.EnsureIPv6();
            return this.networkPeers.Where(n => Match(ip, null, n)).ToList();
        }

        public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            IPAddress ip = endpoint.Address.EnsureIPv6();
            int port = endpoint.Port;
            return this.networkPeers.FirstOrDefault(n => Match(ip, port, n));
        }

        private static bool Match(IPAddress ip, int? port, INetworkPeer peer)
        {
            bool isConnectedOrHandShaked = (peer.State == NetworkPeerState.Connected || peer.State == NetworkPeerState.HandShaked);

            bool isAddressMatching = peer.RemoteSocketAddress.Equals(ip)
                                     && (!port.HasValue || port == peer.RemoteSocketPort);

            bool isPeerVersionAddressMatching = peer.PeerVersion?.AddressFrom != null
                                                && peer.PeerVersion.AddressFrom.Address.Equals(ip)
                                                && (!port.HasValue || port == peer.PeerVersion.AddressFrom.Port);

            return (isConnectedOrHandShaked && isAddressMatching) || isPeerVersionAddressMatching;
        }

        public IEnumerator<INetworkPeer> GetEnumerator()
        {
            return this.networkPeers.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}