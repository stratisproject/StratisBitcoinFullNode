using System;
using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class TestReadOnlyNetworkPeerCollection : IReadOnlyNetworkPeerCollection
    {
        public event EventHandler<NetworkPeerEventArgs> Added;
        public event EventHandler<NetworkPeerEventArgs> Removed;

        private List<INetworkPeer> networkPeers;

        public TestReadOnlyNetworkPeerCollection()
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = new List<INetworkPeer>();
        }

        public TestReadOnlyNetworkPeerCollection(List<INetworkPeer> peers)
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = peers;
        }

        public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            return null;
        }

        public List<INetworkPeer> FindByIp(IPAddress ip)
        {
            return null;
        }

        public INetworkPeer FindLocal()
        {
            return null;
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