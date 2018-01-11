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
        PeerAddress SelectPeer();

        /// <summary>
        /// Select preferred peers from the address manager for sending
        /// via address payload and peer discovery.
        /// </summary>
        /// <param name="peerCount">The amount of peers to return.</param>
        IEnumerable<PeerAddress> SelectPeers(int peerCount);
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

        /// <summary>Random number generator used when selecting and ordering peers.</summary>
        private readonly Random random;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="peerAddresses">The collection of peer address as managed by the peer address manager.</param>
        public PeerSelector(ILoggerFactory loggerFactory, ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddresses = peerAddresses;
            this.random = new Random();
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            this.logger.LogTrace("()");

            PeerAddress peerAddress = null;

            var peers = this.SelectPreferredPeers();
            if (peers.Any())
            {
                peerAddress = Random(peers);
                this.logger.LogTrace("(-):'{0}'", peerAddress.NetworkAddress.Endpoint);
            }
            else
                this.logger.LogTrace("(-)[NO_PEER]");

            return peerAddress;
        }

        /// <summary>
        /// Filtering logic for selecting peers to connect to via the <see cref="PeerConnector"/> classes.
        /// </summary>
        private IEnumerable<PeerAddress> SelectPreferredPeers()
        {
            this.logger.LogTrace("()");

            // First check to see if there are handshaked peers. If so,
            // give them a 50% chance to be picked over all the other peers.
            var handshaked = this.peerAddresses.Handshaked();
            if (handshaked.Any())
            {
                int chance = this.random.Next(100);
                if (chance <= 50)
                {
                    this.logger.LogTrace("(-)[RETURN_HANDSHAKED]");
                    return handshaked;
                }
            }

            // If there are peers that have recently connected, give them
            // a 50% chance to be picked over fresh and/or attempted peers.
            var connected = this.peerAddresses.Connected();
            if (connected.Any())
            {
                int chance = this.random.Next(100);
                if (chance <= 50)
                {
                    this.logger.LogTrace("(-)[RETURN_CONNECTED]");
                    return connected;
                }
            }

            // At this point, if neither selecting handshaked or connected
            // was successful, we will select from fresh or attempted.
            //
            // If both sets exist, pick 50/50 between the two.
            var attempted = this.peerAddresses.Attempted();
            var fresh = this.peerAddresses.Fresh();
            if (attempted.Any() && fresh.Any())
            {
                if (this.random.Next(2) == 0)
                {
                    this.logger.LogTrace("(-)[RETURN_ATTEMPTED]");
                    return attempted;
                }
                else
                {
                    this.logger.LogTrace("(-)[RETURN_FRESH]");
                    return fresh;
                }
            }

            // If there are only fresh peers, return them.
            if (fresh.Any() && !attempted.Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_FRESH_EXIST]");
                return fresh;
            }

            // If there are only attempted peers, return them.
            if (!fresh.Any() && attempted.Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_ATTEMPTED_EXIST]");
                return attempted;
            }

            // If all the selection criteria failed to return a set of peers,
            // then let the caller try again.
            this.logger.LogTrace("(-)[RETURN_NO_PEERS]");
            return new PeerAddress[] { };
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeers(int peerCount)
        {
            // If there are no peers, just one or if the amount peers is less than
            // the amount of peers asked for, just return the list.
            if (!this.peerAddresses.Any() || this.peerAddresses.Count == 1)
                return this.peerAddresses.Select(pa => pa.Value);

            // Randomly order the list of peers and return the amount
            // asked for.
            var allPeers = this.peerAddresses.OrderBy(p => this.random.Next());
            return allPeers.Select(p => p.Value).Take(1000);
        }

        /// <summary>Return a random peer from a given set of peers.</summary>
        private PeerAddress Random(IEnumerable<PeerAddress> peers)
        {
            var random = new Random();

            if (peers.Count() == 1)
                return peers.First();

            var randomPeerIndex = random.Next(peers.Count() - 1);
            var randomPeer = peers.ElementAt(randomPeerIndex);
            return randomPeer;
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
    }
}