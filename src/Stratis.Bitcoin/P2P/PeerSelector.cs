using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;

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
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The address manager instance that holds the peer list to be queried. 
        /// </summary>
        private readonly ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="peerAddresses">The collection of peer address as managed by the peer address manager.</param>
        public PeerSelector(ILoggerFactory loggerFactory, ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.peerAddresses = peerAddresses;
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            this.logger.LogTrace("()");

            PeerAddress peerAddress = null;

            var peers = SelectPeers();
            if (peers.Any())
            {
                peerAddress = peers.Random();
                this.logger.LogTrace("(-):'{0}'", peerAddress.NetworkAddress.Endpoint);
            }
            else
                this.logger.LogTrace("(-)[NO_PEER]");

            return peerAddress;
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeers()
        {
            this.logger.LogTrace("()");

            var random = new Random();

            // First check to see if there are handshaked peers. If so,
            // give them a 75% chance to be picked over all the other peers.
            if (this.peerAddresses.Handshaked().Any())
            {
                var chance = random.Next(100);
                if (chance <= 75)
                {
                    this.logger.LogTrace("(-)[RETURN_HANDSHAKED]");
                    return this.peerAddresses.Handshaked();
                }
            }

            // If there are peers that have recently connected, give them
            // a 75% chance to be picked over fresh and/or attempted peers.
            if (this.peerAddresses.Connected().Any())
            {
                var chance = random.Next(100);
                if (chance <= 75)
                {
                    this.logger.LogTrace("(-)[RETURN_CONNECTED]");
                    return this.peerAddresses.Connected();
                }
            }

            // At this point, if neither selecting handshaked or connected
            // was successful, we will select from fresh or attempted.
            //
            // If both sets exist, pick 50/50 between the two.
            if (this.peerAddresses.Attempted().Any() && this.peerAddresses.Fresh().Any())
            {
                if (random.Next(2) == 0)
                {
                    this.logger.LogTrace("(-)[RETURN_ATTEMPTED]");
                    return this.peerAddresses.Attempted();
                }
                else
                {
                    this.logger.LogTrace("(-)[RETURN_FRESH]");
                    return this.peerAddresses.Fresh();
                }
            }

            // If there are only fresh peers, return them.
            if (this.peerAddresses.Fresh().Any() && !this.peerAddresses.Attempted().Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_FRESH_EXIST]");
                return this.peerAddresses.Fresh();
            }

            // If there are only attempted peers, return them.
            if (!this.peerAddresses.Fresh().Any() && this.peerAddresses.Attempted().Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_ATTEMPTED_EXIST]");
                return this.peerAddresses.Attempted();
            }

            // If all the selection criteria failed to return a set of peers,
            // then let the caller try again.
            this.logger.LogTrace("(-)[RETURN_NO_PEERS]");
            return new PeerAddress[] { };
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