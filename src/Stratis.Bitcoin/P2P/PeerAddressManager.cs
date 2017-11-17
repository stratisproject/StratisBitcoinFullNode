using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities.FileStorage;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The AddressManager keeps a set of peers discovered on the network in cache and on disk.
    /// <para>
    /// The manager updates their states according to how recent they have been connected to.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManager : IDisposable
    {
        private DataFolder peerFilePath;
        internal const string PeerFileName = "peers.json";

        public PeerAddressManager(DataFolder peerFilePath)
        {
            this.peerFilePath = peerFilePath;
            this.Peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
        }

        /// <summary>
        /// A key value store that indexes all discovered peers by their end point.
        /// </summary>
        public ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; private set; }

        public void LoadPeers()
        {
            var fileStorage = new FileStorage<List<PeerAddress>>(this.peerFilePath);
            var peers = fileStorage.WithConverters(new[] { new IPEndpointConverter() }).LoadByFileName(PeerFileName);
            peers.ForEach(peer =>
            {
                this.Peers.TryAdd(peer.NetworkAddress.Endpoint, peer);
            });
        }

        public void SavePeers()
        {
            if (this.Peers.Any() == false)
                return;

            var fileStorage = new FileStorage<List<PeerAddress>>(this.peerFilePath);
            fileStorage.WithConverters(new[] { new IPEndpointConverter() }).SaveToFile(this.Peers.Select(p => p.Value).ToList(), PeerFileName);
        }

        public void AddPeer(NetworkAddress networkAddress, IPAddress source)
        {
            //Specific IP address ranges that are reserved specifically as non - routable addresses to be used in 
            //private networks: 10.0.0.0 through 10.255.255.255. 172.16.0.0 through 172.32.255.255. 192.168.0.0 
            //through 192.168.255.255.
            if (networkAddress.Endpoint.Address.IsRoutable(true) == false)
                return;

            var peerToAdd = PeerAddress.Create(networkAddress, source);
            this.Peers.TryAdd(peerToAdd.NetworkAddress.Endpoint, peerToAdd);
        }

        public void AddPeers(NetworkAddress[] networkAddresses, IPAddress source)
        {
            foreach (var networkAddress in networkAddresses)
            {
                AddPeer(networkAddress, source);
            }
        }

        public void DeletePeer(PeerAddress peer)
        {
            IPEndPoint endPoint = null;
            this.Peers.TryRemove(endPoint, out peer);
        }

        public void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.Attempted(peerAttemptedAt);
        }

        public void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerConnectedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetConnected(peerConnectedAt);
        }

        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetHandshaked(peerHandshakedAt);
        }

        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            var peer = this.Peers.SingleOrDefault(p => p.Value.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <summary>
        /// Selects a random peer to connect to.
        /// <para>
        /// Use a 50% chance for choosing between tried and new peers.
        /// </para>
        /// </summary>
        public NetworkAddress SelectPeerToConnectTo()
        {
            if (this.Peers.Tried().Any() == true &&
                (this.Peers.New().Any() == false || GetRandomInteger(2) == 0))
                return this.Peers.Tried().Random().NetworkAddress;

            if (this.Peers.New().Any() == true)
                return this.Peers.New().Random().NetworkAddress;

            return null;
        }

        /// <summary>
        /// Selects a random set of preferred peers to connects to.
        /// </summary>
        public IEnumerable<NetworkAddress> SelectPeersToConnectTo()
        {
            return this.Peers.Where(p => p.Value.Preferred).Select(p => p.Value.NetworkAddress);
        }

        internal static int GetRandomInteger(int max)
        {
            return (int)(RandomUtils.GetUInt32() % (uint)max);
        }

        public void Dispose()
        {
            SavePeers();
        }
    }

    public static class PeerAddressExtensions
    {
        public static IEnumerable<PeerAddress> New(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            return peers.Skip(0).Where(p => p.Value.IsNew).Select(p => p.Value);
        }

        public static IEnumerable<PeerAddress> Tried(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            return peers.Skip(0).Where(p => !p.Value.IsNew).Select(p => p.Value);
        }

        public static PeerAddress Random(this IEnumerable<PeerAddress> peers)
        {
            //Using "Chance" from NBitcoin-----------------
            double chanceFactor = 1.0;
            while (true)
            {
                if (peers.Count() == 1)
                    return peers.ToArray()[0];

                var randomPeerIndex = PeerAddressManager.GetRandomInteger(peers.Count() - 1);
                var randomPeer = peers.ToArray()[randomPeerIndex];

                if (PeerAddressManager.GetRandomInteger(1 << 30) < chanceFactor * randomPeer.Selectability * (1 << 30))
                    return randomPeer;

                chanceFactor *= 1.2;
            }
            //---------------------------------------------
        }
    }
}