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
        private const double PeerSuccessRatioThreshold = 0.1;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The address manager instance that holds the peer list to be queried. 
        /// </summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// Constructor for the peer selector.
        /// </summary>
        /// <param name="peerAddressManager">The singleton peer address manager instance.</param>
        public PeerSelector(ILoggerFactory loggerFactory, IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddressManager = peerAddressManager;
        }

        /// <inheritdoc/>
        public PeerAddress SelectPeer()
        {
            this.logger.LogTrace("()");

            PeerAddress peerAddress = null;

            var peers = SelectPeers();
            if (peers.Any())
            {
                if (peers.Count() == 1)
                    peerAddress = peers.First();
                else
                    peerAddress = peers.Random();

                this.logger.LogTrace("(-):{0}={1}", nameof(peerAddress), peerAddress.NetworkAddress.Endpoint.ToString());
            }
            else
                this.logger.LogTrace("(-):{0} [NULL]", nameof(peerAddress));

            return peerAddress;
        }

        /// <inheritdoc/>
        public IEnumerable<PeerAddress> SelectPeers()
        {
            this.logger.LogTrace("()");

            // First check to see if there are handshaked peers. If so,
            // give them a 75% chance to be picked over all the other peers.
            if (this.peerAddressManager.Peers.Handshaked().Any() && new Random().Next(5) <= 3)
            {
                this.logger.LogTrace("(-)[RETURN_HANDSHAKED]");
                return this.peerAddressManager.Peers.Handshaked();
            }

            // If there are peers that have recently connected, give them
            // a 75% chance to be picked over fresh and/or attempted peers.
            if (this.peerAddressManager.Peers.Connected().Any() && new Random().Next(5) <= 3)
            {
                this.logger.LogTrace("(-)[RETURN_CONNECTED]");
                return this.peerAddressManager.Peers.Connected();
            }

            // At this point, if neither selecting handshaked or connected
            // was successful, we will select from fresh or attempted.
            //
            // If both sets exist, pick 50/50 between the two.
            if (this.peerAddressManager.Peers.Attempted().Any() && this.peerAddressManager.Peers.Fresh().Any())
            {
                if (new Random().Next(2) == 0)
                {
                    this.logger.LogTrace("(-)[RETURN_ATTEMPTED]");
                    return this.peerAddressManager.Peers.Attempted();
                }
                else
                {
                    this.logger.LogTrace("(-)[RETURN_FRESH]");
                    return this.peerAddressManager.Peers.Fresh();
                }
            }

            // If there are only fresh peers, return them.
            if (this.peerAddressManager.Peers.Fresh().Any() && !this.peerAddressManager.Peers.Attempted().Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_FRESH_EXIST]");
                return this.peerAddressManager.Peers.Fresh();
            }

            // If there are only attempted peers, return them.
            if (!this.peerAddressManager.Peers.Fresh().Any() && this.peerAddressManager.Peers.Attempted().Any())
            {
                this.logger.LogTrace("(-)[RETURN_ONLY_ATTEMPTED_EXIST]");
                return this.peerAddressManager.Peers.Attempted();
            }

            // If all the selection criteria failed to return a set of peers,
            // then let the caller try again.
            this.logger.LogTrace("(-)[RETURN_NO_PEERS]");
            return new PeerAddress[] { };
        }

        private IEnumerable<PeerAddress> SelectFromConnectedOrHandShaked()
        {


            return new PeerAddress[] { };
        }

        /// <summary>
        /// Pick a peer from the attempted/successful list of peers.
        /// </summary>
        private IEnumerable<PeerAddress> SelectPeersFromAttempedOrSuccessful()
        {
            var prefferedAttempted = this.peerAddressManager.Peers.Attempted().Where(p =>
                                        p.ConnectionAttempts <= 10 &&
                                        p.LastConnectionAttempt < DateTimeOffset.Now.AddSeconds(-60));

            var prefferedSucceeded = this.peerAddressManager.Peers.Connected().Where(p =>
                                        p.LastConnectionSuccess < DateTimeOffset.Now.AddSeconds(-60) &&
                                        p.LastConnectionSuccess > DateTimeOffset.Now.AddDays(-30));

            // If there are attempted AND successful peers, we need to randomly
            // pick between the two.
            //
            // HOWEVER, if the ratio of successful to attempted peers is less 
            // than 10%, we can't just randomly do a 50/50 pick.
            //
            // Imagine a scenario where the peer set is split between a 1000 attempted
            // ones and only 20 has had successful attempts. If we 50/50 pick between 
            // these two it will take quite a while for the address manager to 
            // return a preferred peer (we want to start connecting to successful)
            // peers as soon as we can.
            //
            // To achieve this we check a success ratio and if its below a threshold,
            // we only pick from the success peers.
            //
            // Over time the sucessful peer set will grow, causing the ratio
            // to increase, thus allowing 50/50 selection from the two sets again.
            //
            // I.e. the address manager will try and restore some balance to the 
            // amount of attempted and successful peers.
            if (prefferedSucceeded.Any())
            {
                var successfullRatio = (double)prefferedSucceeded.Count() / (double)prefferedAttempted.Count();
                if (successfullRatio < PeerSuccessRatioThreshold)
                    return prefferedSucceeded;

                var random = new Random().Next(2);
                return random == 0 ? prefferedAttempted : prefferedSucceeded;
            }
            else
                return prefferedAttempted;
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