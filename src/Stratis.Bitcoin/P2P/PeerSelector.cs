using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

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
        /// Return peers which have had connection attempts, but none successful.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the
        /// last 60 seconds and that has had more than 10 failed attempts.
        /// </para>
        /// </summary>
        IEnumerable<PeerAddress> Attempted();

        /// <summary>
        /// Return peers which have had successful connection attempts.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the
        /// last 60 seconds.
        /// </para>
        /// </summary>
        /// <param name="throttlePeriodSeconds">Filter results that connected within this time frame.</param>
        IEnumerable<PeerAddress> Connected(int throttlePeriodSeconds = 60);

        /// <summary>Returns peers that are not banned.</summary>
        IEnumerable<PeerAddress> NotBanned();

        /// <summary>
        /// Return peers which have never had connection attempts.
        /// </summary>
        IEnumerable<PeerAddress> Fresh();

        /// <summary>
        /// Return peers where a successful connection and handshake was achieved.
        /// <para>
        /// The result filters out peers which satisfies the above condition within the
        /// last 60 seconds.
        /// </para>
        /// </summary>
        /// <param name="throttlePeriodSeconds">Filter results that hand shaked within this time frame.</param>
        IEnumerable<PeerAddress> Handshaked(int throttlePeriodSeconds = 60);

        /// <summary>
        /// <para>
        /// Filter peers by handshake failures.
        /// </para>
        /// </summary>
        IEnumerable<PeerAddress> FilterBadHandshakedPeers(IEnumerable<PeerAddress> peers);

        /// <summary>Determines whether all not banned peers reached connection attempts threshold.</summary>
        bool HasAllPeersReachedConnectionThreshold();

        /// <summary>Resets connection attempts count for all not banned peers.</summary>
        void ResetConnectionAttemptsOnNotBannedPeers();
    }

    [NoTrace]
    public sealed class PeerSelector : IPeerSelector
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

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

        /// <summary>Keeps track of and allows querying whether an IPEndoint is itself.</summary>
        private readonly ISelfEndpointTracker selfEndpointTracker;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of datetime.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        /// <param name="peerAddresses">The collection of peer address as managed by the peer address manager.</param>
        /// <param name="selfEndpointTracker">Self endpoint tracker.</param>
        public PeerSelector(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, ConcurrentDictionary<IPEndPoint, PeerAddress> peerAddresses, ISelfEndpointTracker selfEndpointTracker)
        {
            this.selfEndpointTracker = selfEndpointTracker;
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddresses = peerAddresses;
            this.random = new Random();
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            PeerAddress peerAddress = null;

            List<PeerAddress> peers = this.SelectPreferredPeers()
                .Where(p => !this.selfEndpointTracker.IsSelf(p.Endpoint))
                .ToList();

            if (peers.Any())
            {
                peerAddress = this.Random(peers);
            }
            else
                this.logger.LogTrace("There is no peer.");

            return peerAddress;
        }

        /// <summary>
        /// Filtering logic for selecting peers to connect to via the <see cref="PeerConnector"/> classes.
        /// </summary>
        private IEnumerable<PeerAddress> SelectPreferredPeers()
        {
            // First check to see if there are handshaked peers. If so,
            // give them a 50% chance to be picked over all the other peers.
            List<PeerAddress> handshaked = this.Handshaked().ToList();
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
            List<PeerAddress> connected = this.Connected().ToList();
            if (connected.Any())
            {
                connected = this.FilterBadHandshakedPeers(connected).ToList();
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
            List<PeerAddress> attempted = this.Attempted().ToList();
            List<PeerAddress> fresh = this.Fresh().ToList();
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

            if (this.HasAllPeersReachedConnectionThreshold())
                this.ResetConnectionAttemptsOnNotBannedPeers();

            // If all the selection criteria failed to return a set of peers, then let the caller try again.
            this.logger.LogTrace("(-)[RETURN_NO_PEERS]");
            return new PeerAddress[] { };
        }

        /// <inheritdoc/>
        public bool HasAllPeersReachedConnectionThreshold()
        {
            IEnumerable<PeerAddress> notBanned = this.NotBanned();

            int attemptedReachedThresholdCount = notBanned.Count(p => p.ConnectionAttempts == PeerAddress.AttemptThreshold);
            bool areAllPeersReachedThreshold = attemptedReachedThresholdCount == notBanned.Count();

            return areAllPeersReachedThreshold;
        }

        /// <inheritdoc/>
        public void ResetConnectionAttemptsOnNotBannedPeers()
        {
            List<PeerAddress> notBanned = this.NotBanned().ToList();
            this.logger.LogTrace("Resetting attempts for {0} addresses.", notBanned.Count);

            // Reset attempts for all the peers since we've ran out of options.
            foreach (PeerAddress peer in notBanned)
                peer.ResetAttempts();
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeersForDiscovery(int peerCount)
        {
            if (this.peerAddresses.Count == 0)
                return Enumerable.Empty<PeerAddress>();

            var discoverable = this.peerAddresses.Values.Where(pa => pa.LastDiscoveredFrom == null).ToList();
            var filteredDiscoverable = this.peerAddresses.Values.Where(p => p.LastDiscoveredFrom != null && p.LastDiscoveredFrom < this.dateTimeProvider.GetUtcNow().AddHours(-PeerSelector.DiscoveryThresholdHours));
            discoverable.AddRange(filteredDiscoverable);

            discoverable = discoverable
                            .Where(p => !this.selfEndpointTracker.IsSelf(p.Endpoint))
                            .Where(p => !this.IsBanned(p))
                            .ToList();

            return discoverable.OrderBy(p => this.random.Next()).Take(1000).ToList();
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

            List<PeerAddress> connectedAndHandshaked = this.Connected(0).Concat(this.Handshaked(0)).OrderBy(p => this.random.Next()).ToList();
            List<PeerAddress> freshAndAttempted = this.Attempted().Concat(this.Fresh()).OrderBy(p => this.random.Next()).ToList();

            // If there are connected and/or handshaked peers in the address list,
            // we need to split the list 50 / 50 between them and
            // peers we have not yet connected to and/or that are fresh.
            if (connectedAndHandshaked.Any())
            {
                // 50% of the peers to return
                int toTake = peerCount / 2;

                // If the amount of connected and/or handshaked peers is less
                // than 50% of the peers asked for, just take all of them.
                if (connectedAndHandshaked.Count() < toTake)
                    peersToReturn.AddRange(connectedAndHandshaked);
                // If not take 50% of the amount requested.
                else
                    peersToReturn.AddRange(connectedAndHandshaked.Take(toTake));

                // Fill up the list with the rest.
                peersToReturn.AddRange(freshAndAttempted.Take(peerCount - peersToReturn.Count()));
            }

            // If there are no connected or handshaked peers in the address list,
            // just return an amount of peers that has been asked for.
            else
            {
                peersToReturn.AddRange(freshAndAttempted.Take(peerCount));
            }

            return peersToReturn;
        }

        /// <summary>Return a random peer from a given set of peers.</summary>
        [NoTrace]
        private PeerAddress Random(IEnumerable<PeerAddress> peers)
        {
            if (peers.Count() == 1)
                return peers.First();

            int randomPeerIndex = this.random.Next(peers.Count());
            PeerAddress randomPeer = peers.ElementAt(randomPeerIndex);
            return randomPeer;
        }

        /// <inheritdoc/>
        [NoTrace]
        public IEnumerable<PeerAddress> Attempted()
        {
            return this.peerAddresses.Values.Where(p =>
                p.Attempted &&
                p.ConnectionAttempts < PeerAddress.AttemptThreshold &&
                p.LastAttempt < this.dateTimeProvider.GetUtcNow().AddHours(-PeerAddress.AttempThresholdHours) &&
                !this.IsBanned(p));
        }

        /// <inheritdoc/>
        [NoTrace]
        public IEnumerable<PeerAddress> FilterBadHandshakedPeers(IEnumerable<PeerAddress> peers)
        {
            IEnumerable<PeerAddress> filteredPeers = peers.Where(p => (p.HandshakedAttempts < PeerAddress.AttemptHandshakeThreshold) ||
                                    p.LastHandshakeAttempt?.AddHours(PeerAddress.AttempThresholdHours) < this.dateTimeProvider.GetUtcNow()).ToList();

            foreach (PeerAddress peer in filteredPeers)
            {
                if (peer.HandshakedAttempts == PeerAddress.AttemptHandshakeThreshold)
                    peer.ResetHandshakeAttempts();
            }

            return filteredPeers;
        }

        [NoTrace]
        public IEnumerable<PeerAddress> NotBanned()
        {
            return this.peerAddresses.Values.Where(p => !this.IsBanned(p));
        }

        /// <inheritdoc/>
        [NoTrace]
        public IEnumerable<PeerAddress> Connected(int throttlePeriodSeconds = 60)
        {
            var result = this.peerAddresses.Values
                .Where(p => p.Connected && !this.IsBanned(p))
                .Where(p => p.LastConnectionSuccess < this.dateTimeProvider.GetUtcNow().AddSeconds(-throttlePeriodSeconds));
            return result;
        }

        /// <inheritdoc/>
        [NoTrace]
        public IEnumerable<PeerAddress> Fresh()
        {
            return this.peerAddresses.Values.Where(p => p.Fresh && !this.IsBanned(p));
        }

        /// <inheritdoc/>
        [NoTrace]
        public IEnumerable<PeerAddress> Handshaked(int throttlePeriodSeconds = 60)
        {
            var result = this.peerAddresses.Values
                .Where(p => p.Handshaked && !this.IsBanned(p))
                .Where(p => p.LastConnectionHandshake < this.dateTimeProvider.GetUtcNow().AddSeconds(-throttlePeriodSeconds));
            return result;
        }

        /// <summary>
        /// <c>True</c> if <see cref="PeerAddress.BanUntil"/> is in the future.
        /// </summary>
        [NoTrace]
        private bool IsBanned(PeerAddress peerAddress)
        {
            return peerAddress.BanUntil > this.dateTimeProvider.GetUtcNow();
        }
    }
}