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
            if (this.peerAddressManager.Peers.Tried().Any() == true &&
                (this.peerAddressManager.Peers.New().Any() == false || new Random().Next(2) == 0))
                return this.peerAddressManager.Peers.Tried().Random();

            if (this.peerAddressManager.Peers.New().Any() == true)
                return this.peerAddressManager.Peers.New().Random();

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
        /// Return peers where they have never had a connection attempt or have been connected to.
        /// </summary>
        public static IEnumerable<PeerAddress> New(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var isNew = peers.Skip(0).Where(p => p.Value.IsNew).Select(p => p.Value);
            return isNew;
        }

        /// <summary>
        /// Return peers where they have had connection attempts, successful or not.
        /// </summary>
        public static IEnumerable<PeerAddress> Tried(this ConcurrentDictionary<IPEndPoint, PeerAddress> peers)
        {
            var tried = peers.Skip(0).Where(p => !p.Value.IsNew).Select(p => p.Value);

            return tried;
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