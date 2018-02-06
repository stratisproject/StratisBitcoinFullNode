using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
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
        PeerAddress SelectPeer();

        /// <summary>
        /// Select a random set of peers from the address manager for peer discovery.
        /// </summary>
        /// <param name="peerCount">The amount of peers to return.</param>
        IEnumerable<PeerAddress> SelectPeersForDiscovery(int peerCount);

        /// <summary>
        /// Select preferred peers from the address manager for sending
        /// via address payload.
        /// </summary>
        /// <param name="peerCount">The amount of peers to return.</param>
        IEnumerable<PeerAddress> SelectPeersForGetAddrPayload(int peerCount);

        /// <summary>
        /// Return peers which've had connection attempts but none successful. 
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds and that has had more than 10 failed attempts.
        /// </para>
        /// </summary>
        IEnumerable<PeerAddress> Attempted();

        /// <summary>
        /// Return peers which've had successful connection attempts.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds.
        /// </para>
        /// </summary>
        IEnumerable<PeerAddress> Connected();

        /// <summary>
        /// Return peers which've never had connection attempts. 
        /// </summary>
        IEnumerable<PeerAddress> Fresh();

        /// <summary>
        /// Return peers where a successful connection and handshake was achieved.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the 
        /// last 60 seconds.
        /// </para>
        /// </summary>
        IEnumerable<PeerAddress> Handshaked();
    }

    public sealed class PeerSelector : IPeerSelector
    {
        /// <summary>The amount of hours we should wait before we try and discover from a peer again.</summary>
        private const int DiscoveryThresholdHours = 24;

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

            var peers = this.SelectPreferredPeers().ToList();
            if (peers.Any())
            {
                peerAddress = Random(peers);
                this.logger.LogTrace("(-):'{0}'", peerAddress.Endpoint);
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
            // First check to see if there are handshaked peers. If so,
            // give them a 50% chance to be picked over all the other peers.
            var handshaked = this.Handshaked().ToList();
            if (handshaked.Any())
            {
                int chance = this.random.Next(100);
                if (chance <= 50)
                {
                    this.logger.LogTrace("[RETURN_HANDSHAKED]");
                    return handshaked;
                }
            }

            // If there are peers that have recently connected, give them
            // a 50% chance to be picked over fresh and/or attempted peers.
            var connected = this.Connected().ToList();
            if (connected.Any())
            {
                int chance = this.random.Next(100);
                if (chance <= 50)
                {
                    this.logger.LogTrace("[RETURN_CONNECTED]");
                    return connected;
                }
            }

            // At this point, if neither selecting handshaked or connected
            // was successful, we will select from fresh or attempted.
            //
            // If both sets exist, pick 50/50 between the two.
            var attempted = this.Attempted().ToList();
            var fresh = this.Fresh().ToList();
            if (attempted.Any() && fresh.Any())
            {
                if (this.random.Next(2) == 0)
                {
                    this.logger.LogTrace("[RETURN_ATTEMPTED]");
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
                this.logger.LogTrace("[RETURN_FRESH_HC_FAILED]");
                return fresh;
            }

            // If there are only attempted peers, return them.
            if (!fresh.Any() && attempted.Any())
            {
                this.logger.LogTrace("[RETURN_ATTEMPTED_HC_FAILED]");
                return attempted;
            }

            // If all the selection criteria failed to return a set of peers,
            // then let the caller try again.
            else
                this.logger.LogTrace("[RETURN_NO_PEERS]");

            return new PeerAddress[] { };
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeersForDiscovery(int peerCount)
        {
            var discoverable = this.peerAddresses.Values.Where(p => p.LastDiscoveredFrom < DateTimeProvider.Default.GetUtcNow().AddHours(-PeerSelector.DiscoveryThresholdHours));
            var allPeers = discoverable.Take(1000).ToList();
            return allPeers.OrderBy(p => this.random.Next());
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeersForGetAddrPayload(int peerCount)
        {
            // If there are no peers return an empty list.
            if (!this.peerAddresses.Any())
                return this.peerAddresses.Values;

            // If there's one peer then just return the list.
            if (this.peerAddresses.Count == 1)
                return this.peerAddresses.Values;

            var peersToReturn = new List<PeerAddress>();

            var connectedAndHandshaked = this.Connected().Concat(this.Handshaked()).OrderBy(p => this.random.Next()).ToList();
            var freshAndAttempted = this.Attempted().Concat(this.Fresh()).OrderBy(p => this.random.Next()).ToList();

            //If there are connected and/or handshaked peers in the address list,
            //we need to split the list 50 / 50 between them and
            //peers we have not yet connected to and/or that are fresh.
            if (connectedAndHandshaked.Any())
            {
                //50% of the peers to return
                var toTake = peerCount / 2;

                //If the amount of connected and/or handshaked peers is less
                //than 50% of the peers asked for, just take all of them.
                if (connectedAndHandshaked.Count() < toTake)
                    peersToReturn.AddRange(connectedAndHandshaked);
                //If not take 50% of the amount requested.
                else
                    peersToReturn.AddRange(connectedAndHandshaked.Take(toTake));

                //Fill up the list with the rest.
                peersToReturn.AddRange(freshAndAttempted.Take(peerCount - peersToReturn.Count()));
            }

            //If there are no connected or handshaked peers in the address list,
            //just return an amount of peers that has been asked for. 
            else
            {
                peersToReturn.AddRange(freshAndAttempted.Take(peerCount));
            }

            return peersToReturn;
        }

        /// <summary>Return a random peer from a given set of peers.</summary>
        private PeerAddress Random(IEnumerable<PeerAddress> peers)
        {
            if (peers.Count() == 1)
                return peers.First();

            var randomPeerIndex = this.random.Next(peers.Count() - 1);
            var randomPeer = peers.ElementAt(randomPeerIndex);
            return randomPeer;
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> Attempted()
        {
            return this.peerAddresses.Values.Where(p =>
                                p.Attempted &&
                                p.ConnectionAttempts < PeerAddress.AttemptThreshold &&
                                p.LastAttempt < DateTime.UtcNow.AddHours(-PeerAddress.AttempThresholdHours));
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> Connected()
        {
            return this.peerAddresses.Values.Where(p => p.Connected && p.LastConnectionSuccess < DateTime.UtcNow.AddSeconds(-60));
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> Fresh()
        {
            return this.peerAddresses.Values.Where(p => p.Fresh);
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> Handshaked()
        {
            return this.peerAddresses.Values.Where(p => p.Handshaked && p.LastConnectionHandshake < DateTime.UtcNow.AddSeconds(-60));
        }
    }
}