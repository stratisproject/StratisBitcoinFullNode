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
    /// <summary>Contract for <see cref="PeerAddressManager"/>.</summary>
    public interface IPeerAddressManager : IDisposable
    {
        /// <summary>Data folder of where the json peer file is located.</summary>
        DataFolder PeerFilePath { get; set; }

        /// <summary>Key value store that indexes all discovered peers by their end point.</summary>
        ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; }

        /// <summary>
        /// Adds a peer to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>
        /// </para>
        /// </summary>
        void AddPeer(NetworkAddress networkAddress, IPAddress source, PeerIntroductionType peerIntroductionType);

        /// <summary>
        /// Add a set of peers to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>
        /// </para>
        /// </summary>
        void AddPeers(NetworkAddress[] networkAddress, IPAddress source, PeerIntroductionType introductionType);

        /// <summary> Find a peer by endpoint.</summary>
        PeerAddress FindPeer(IPEndPoint endPoint);

        /// <summary>Loads peers from a json formatted file on disk.</summary>
        void LoadPeers();

        /// <summary>Persist peers to disk in json format.</summary>
        void SavePeers();

        /// <summary>
        /// A connection attempt was made to a peer.
        /// <para>
        /// Increments <see cref="PeerAddress.ConnectionAttempts"/> of the peer as well as the <see cref="PeerAddress.LastConnectionSuccess"/>
        /// </para>
        /// </summary>
        void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// A peer was successfully connected to.
        /// <para>
        /// Resets the <see cref="PeerAddress.ConnectionAttempts"/> and <see cref="PeerAddress.LastConnectionAttempt"/> of the peer.
        /// Sets the peer's <see cref="PeerAddress.LastConnectionSuccess"/> to now.
        /// </para>
        /// </summary>
        void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// A version handshake between two peers was successful.
        /// <para>
        /// Sets the peer's <see cref="PeerAddress.LastConnectionHandshake"/> time to now.
        /// </para>
        /// </summary>
        void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// Selects a random peer (by <see cref="PeerIntroductionType"/>) to connect to.
        /// <para>
        /// Use a 50% chance for choosing between tried and new peers.
        /// </para>
        /// </summary>
        NetworkAddress SelectPeerToConnectTo(PeerIntroductionType peerIntroductionType);

        /// <summary>
        /// Selects a random set of preferred peers to connects to.
        /// <para>
        /// See <see cref="PeerAddressExtensions.Random(IEnumerable{PeerAddress})"/>.
        /// </para>
        /// </summary>
        IEnumerable<NetworkAddress> SelectPeersToConnectTo();
    }

    /// <summary>
    /// This manager keeps a set of peers discovered on the network in cache and on disk.
    /// <para>
    /// The manager updates their states according to how recent they have been connected to.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManager : IPeerAddressManager
    {
        public PeerAddressManager()
        {
            this.Peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
        }

        public PeerAddressManager(DataFolder peerFilePath) : this()
        {
            this.PeerFilePath = peerFilePath;
        }

        /// <inheritdoc />
        public ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; private set; }

        internal const string PeerFileName = "peers.json";
        public DataFolder PeerFilePath { get; set; }

        /// <inheritdoc />
        public void LoadPeers()
        {
            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath);
            var peers = fileStorage.WithConverters(new[] { new IPEndPointConverter() }).LoadByFileName(PeerFileName);
            peers.ForEach(peer =>
            {
                peer.PeerIntroductionType = PeerIntroductionType.Discover;
                this.Peers.TryAdd(peer.NetworkAddress.Endpoint, peer);
            });
        }

        /// <inheritdoc />
        public void SavePeers()
        {
            if (this.Peers.Any() == false)
                return;

            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath);
            fileStorage.WithConverters(new[] { new IPEndPointConverter() }).SaveToFile(this.Peers.Select(p => p.Value).ToList(), PeerFileName);
        }

        /// <inheritdoc/>
        public void AddPeer(NetworkAddress networkAddress, IPAddress source, PeerIntroductionType peerIntroductionType)
        {
            if (networkAddress.Endpoint.Address.IsRoutable(true) == false)
                return;

            var peerToAdd = PeerAddress.Create(networkAddress, source, peerIntroductionType);
            this.Peers.TryAdd(peerToAdd.NetworkAddress.Endpoint, peerToAdd);
        }

        /// <inheritdoc/>
        public void AddPeers(NetworkAddress[] networkAddresses, IPAddress source, PeerIntroductionType peerIntroductionType)
        {
            foreach (var networkAddress in networkAddresses)
            {
                this.AddPeer(networkAddress, source, peerIntroductionType);
            }
        }

        /// <inheritdoc/>
        public void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.Attempted(peerAttemptedAt);
        }

        /// <inheritdoc/>
        public void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerConnectedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetConnected(peerConnectedAt);
        }

        /// <inheritdoc/>
        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetHandshaked(peerHandshakedAt);
        }

        /// <inheritdoc/>
        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            var peer = this.Peers.SingleOrDefault(p => p.Value.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <inheritdoc />
        public NetworkAddress SelectPeerToConnectTo(PeerIntroductionType peerIntroductionType)
        {
            if (this.Peers.Tried(peerIntroductionType).Any() == true &&
                (this.Peers.New(peerIntroductionType).Any() == false || GetRandomInteger(2) == 0))
                return this.Peers.Tried(peerIntroductionType).Random().NetworkAddress;

            if (this.Peers.New(peerIntroductionType).Any() == true)
                return this.Peers.New(peerIntroductionType).Random().NetworkAddress;

            return null;
        }

        /// <inheritdoc />
        public IEnumerable<NetworkAddress> SelectPeersToConnectTo()
        {
            return this.Peers.Where(p => p.Value.Preferred).Select(p => p.Value.NetworkAddress);
        }

        internal static int GetRandomInteger(int max)
        {
            return (int)(RandomUtils.GetUInt32() % (uint)max);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SavePeers();
        }
    }

    public static class PeerAddressExtensions
    {
        /// <summary>
        /// Return peers where they have never have had a connection attempt or have been connected to.
        /// </summary>
        public static IEnumerable<PeerAddress> New(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers, PeerIntroductionType peerIntroductionType)
        {
            var isNew = peers.Skip(0)
                .Where(p => p.Value.PeerIntroductionType == peerIntroductionType)
                .Where(p => p.Value.IsNew).Select(p => p.Value);
            return isNew;
        }

        /// <summary>
        /// Return peers where they have have had connection attempts, successful or not.
        /// </summary>
        public static IEnumerable<PeerAddress> Tried(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers, PeerIntroductionType peerIntroductionType)
        {
            var tried = peers.Skip(0)
                .Where(p => p.Value.PeerIntroductionType == peerIntroductionType)
                .Where(p => !p.Value.IsNew).Select(p => p.Value);

            return tried;
        }

        /// <summary>Return a random peer from a given set of peers.</summary>
        public static PeerAddress Random(this IEnumerable<PeerAddress> peers)
        {
            var chanceFactor = 1.0;
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
        }
    }
}