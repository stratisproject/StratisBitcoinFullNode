using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Contract for <see cref="PeerSelector"/>.
    /// </summary>
    public interface IPeerSelector
    {
        /// <summary>
        /// Selects a random peer, via a selection algorithm, from the address 
        /// manager to connect to.
        /// </summary>
        /// <remarks>
        /// See the inline comments for specifics on how the peer is selected.
        /// </remarks>
        PeerAddress SelectPeer();

        /// <summary>
        /// Select preferred peers from the address manager for peer discovery.
        /// </summary>
        /// <remarks>
        /// See the inline comments for specifics on how the peers are selected.
        /// </remarks>
        IEnumerable<PeerAddress> SelectPeers();
    }

    public sealed class PeerSelector : IPeerSelector
    {
        /// <summary>
        /// The address manager instance that holds the peer list to be queried. 
        /// </summary>
        private readonly ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="peerAddresses">
        /// The collection of peer address as managed by the
        /// peer address manager.
        /// </param>
        public PeerSelector(ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses)
        {
            Guard.NotNull(peerAddresses, nameof(peerAddresses));

            this.peerAddresses = peerAddresses;
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            var tried = this.peerAddresses.Attempted().Concat(this.peerAddresses.Connected());

            if (tried.Any() == true && (!this.peerAddresses.Fresh().Any() || new Random().Next(2) == 0))
                return tried.Random();

            if (this.peerAddresses.Fresh().Any())
                return this.peerAddresses.Fresh().Random();

            return null;
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeers()
        {
            return this.peerAddresses.Where(p => p.Value.Preferred).Select(p => p.Value);
        }
    }

    public static class PeerSelectorExtensions
    {
        /// <summary>
        /// Return peers which've had connection attempts but none successful. 
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds and that has had more than 10 failed attempts.
        /// </para>
        /// </summary>
        public static IEnumerable<PeerAddress> Attempted(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p =>
                                p.Value.Attempted &&
                                p.Value.ConnectionAttempts <= 10 &&
                                p.Value.LastConnectionAttempt < DateTime.UtcNow.AddSeconds(-60)).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return peers which've had successful connection attempts.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds.
        /// </para>
        /// </summary>
        public static IEnumerable<PeerAddress> Connected(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p =>
                                p.Value.Connected &&
                                p.Value.LastConnectionSuccess < DateTime.UtcNow.AddSeconds(-60)).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return peers which've never had connection attempts. 
        /// </summary>
        public static IEnumerable<PeerAddress> Fresh(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p => p.Value.Fresh).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return peers where a successful connection and handshake was achieved.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds.
        /// </para>
        /// </summary>
        public static IEnumerable<PeerAddress> Handshaked(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p =>
                                p.Value.Handshaked &&
                                p.Value.LastConnectionHandshake < DateTime.UtcNow.AddSeconds(-60)).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return a random peer from a given set of peers.
        /// </summary>
        public static PeerAddress Random(this IEnumerable<PeerAddress> peers)
        {
            var random = new Random();

            if (peers.Count() == 1)
                return peers.First();

            var randomPeerIndex = random.Next(peers.Count() - 1);
            var randomPeer = peers.ToArray()[randomPeerIndex];
            return randomPeer;
        }
    }
}