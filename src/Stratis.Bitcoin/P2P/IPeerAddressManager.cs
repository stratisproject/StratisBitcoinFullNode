using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Contract for <see cref="PeerAddressManager"/>.</summary>
    public interface IPeerAddressManager : IDisposable
    {
        /// <summary>Data folder of where the json peer file is located.</summary>
        DataFolder PeerFilePath { get; set; }

        /// <summary>A collection of all discovered peers.</summary>
        ICollection<PeerAddress> Peers { get; }

        /// <summary>
        /// Adds a peer to the <see cref="Peers"/> dictionary if it doesn't exist yet.
        /// <para>
        /// Only routable IP addresses will be added. See <see cref="IpExtensions.IsRoutable"/>.
        /// </para>
        /// </summary>
        void AddPeer(IPEndPoint endPoint, IPAddress source);

        /// <summary>
        /// Add a set of peers to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>
        /// </para>
        /// </summary>
        void AddPeers(IEnumerable<IPEndPoint> endPoints, IPAddress source);

        /// <summary>
        /// Remove a peer from the <see cref="Peers"/> dictionary.
        /// </summary>
        void RemovePeer(IPEndPoint endPoint);

        /// <summary>Find a peer by endpoint.</summary>
        PeerAddress FindPeer(IPEndPoint endPoint);

        /// <summary>Find peers by IP (the port is irrelevant).</summary>
        List<PeerAddress> FindPeersByIp(IPEndPoint endPoint);

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
        void PeerAttempted(IPEndPoint endpoint, DateTime peerAttemptedAt);

        /// <summary>
        /// A peer was successfully connected to.
        /// <para>
        /// Resets the <see cref="PeerAddress.ConnectionAttempts"/> and <see cref="PeerAddress.LastAttempt"/> of the peer.
        /// Sets the peer's <see cref="PeerAddress.LastConnectionSuccess"/> to now.
        /// </para>
        /// </summary>
        void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// Sets the last time the peer was asked for addresses via discovery.
        /// </summary>
        void PeerDiscoveredFrom(IPEndPoint endpoint, DateTime peerDiscoveredFrom);

        /// <summary>
        /// A version handshake between two peers was successful.
        /// <para>
        /// Sets the peer's <see cref="PeerAddress.LastConnectionHandshake"/> time to now.
        /// </para>
        /// </summary>
        void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt);

        /// <summary>
        /// Sets the last time the peer was seen.
        /// </summary>
        void PeerSeen(IPEndPoint endpoint, DateTime peerSeenAt);

        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        IPeerSelector PeerSelector { get; }
    }
}