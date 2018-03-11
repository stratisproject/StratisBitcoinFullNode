﻿using System;
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
        public INetworkPeer peer { get; private set; }

        public NetworkPeerEventArgs(INetworkPeer peer, bool added)
        {
            this.Added = added;
            this.peer = peer;
        }
    }

    public interface IReadOnlyNetworkPeerCollection : IEnumerable<INetworkPeer>
    {
        INetworkPeer FindByEndpoint(IPEndPoint endpoint);
        INetworkPeer FindByIp(IPAddress ip);
        INetworkPeer FindLocal();
    }

    public class NetworkPeerCollection : IEnumerable<INetworkPeer>, IReadOnlyNetworkPeerCollection
    {
        private ConcurrentHashSet<INetworkPeer> networkPeers;

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
            return this.FindByIp(IPAddress.Loopback);
        }

        public INetworkPeer FindByIp(IPAddress ip)
        {
            ip = ip.EnsureIPv6();
            return this.networkPeers.FirstOrDefault(n => Match(ip, null, n));
        }

        public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            IPAddress ip = endpoint.Address.EnsureIPv6();
            int port = endpoint.Port;
            return this.networkPeers.FirstOrDefault(n => Match(ip, port, n));
        }

        private static bool Match(IPAddress ip, int? port, INetworkPeer peer)
        {
            if (port.HasValue)
            {
                return ((peer.State == NetworkPeerState.Connected || peer.State == NetworkPeerState.HandShaked) && peer.RemoteSocketAddress.Equals(ip) && 
                        (peer.RemoteSocketPort == port.Value)) || (peer.PeerVersion.AddressFrom.Address.Equals(ip) && (peer.PeerVersion.AddressFrom.Port == port.Value));
            }
            else
            {
                return ((peer.State == NetworkPeerState.Connected || peer.State == NetworkPeerState.HandShaked) && 
                        peer.RemoteSocketAddress.Equals(ip)) || peer.PeerVersion.AddressFrom.Address.Equals(ip);
            }
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