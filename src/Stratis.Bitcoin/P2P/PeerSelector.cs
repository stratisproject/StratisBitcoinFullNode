using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Contract interface for <see cref="PeerSelector"/>.
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
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="peerAddressManager">The singleton peer address manager instance.</param>
        public PeerSelector(IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.peerAddressManager = peerAddressManager;
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            var tried = this.peerAddressManager.Peers.Attempted().Concat(this.peerAddressManager.Peers.Connected());

            if (tried.Any() == true && (!this.peerAddressManager.Peers.Fresh().Any() || new Random().Next(2) == 0))
                return tried.Random();

            if (this.peerAddressManager.Peers.Fresh().Any())
                return this.peerAddressManager.Peers.Fresh().Random();

            return null;
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeers()
        {
            return this.peerAddressManager.Peers.Where(p => p.Value.Preferred).Select(p => p.Value);
        }
    }

    public static class PeerSelectorExtensions
    {
        /// <summary>
        /// Return peers which've had connection attempts but none successful. 
        /// </summary>
        public static IEnumerable<PeerAddress> Attempted(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p => p.Value.Attempted).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return peers which've had successful connection attempts.
        /// </summary>
        public static IEnumerable<PeerAddress> Connected(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p => p.Value.Connected).Select(p => p.Value);
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
        /// </summary>
        public static IEnumerable<PeerAddress> Handshaked(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var result = peers.Skip(0).Where(p => p.Value.Handshaked).Select(p => p.Value);
            return result;
        }

        /// <summary>
        /// Return a random peer from a given set of peers.
        /// </summary>
        public static PeerAddress Random(this IEnumerable<PeerAddress> peers)
        {
            var chanceFactor = 1.0;
            var random = new Random();

            while (true)
            {
                if (peers.Count() == 1)
                    return peers.ToArray()[0];

                var randomPeerIndex = random.Next(peers.Count() - 1);
                var randomPeer = peers.ToArray()[randomPeerIndex];

                if (random.Next(1 << 30) < chanceFactor * randomPeer.Selectability * (1 << 30))
                    return randomPeer;

                chanceFactor *= 1.2;
            }
        }
    }
}