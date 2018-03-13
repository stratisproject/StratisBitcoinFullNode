﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

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
        /// Only routable IP addresses will be added. See <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>.
        /// </para>
        /// </summary>
        void AddPeer(IPEndPoint endPoint, IPAddress source);

        /// <summary>
        /// Add a set of peers to the <see cref="Peers"/> dictionary.
        /// <para>
        /// Only routable IP addresses will be added. <see cref="IpExtensions.IsRoutable(IPAddress, bool)"/>
        /// </para>
        /// </summary>
        void AddPeers(IPEndPoint[] endPoints, IPAddress source);

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

    /// <summary>
    /// This manager keeps a set of peers discovered on the network in cache and on disk.
    /// <para>
    /// The manager updates peer state according to how recent they have been connected to or not.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManager : IPeerAddressManager
    {
        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <inheritdoc />
        public ConcurrentDictionary<IPEndPoint, PeerAddress> Peers { get; private set; }

        /// <summary>The file name of the peers file.</summary>
        internal const string PeerFileName = "peers.json";

        /// <inheritdoc />
        public DataFolder PeerFilePath { get; set; }

        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        public IPeerSelector PeerSelector { get; private set; }

        /// <summary>Constructor used by dependency injection.</summary>
        public PeerAddressManager(IDateTimeProvider dateTimeProvider, DataFolder peerFilePath, ILoggerFactory loggerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            this.PeerFilePath = peerFilePath;
            this.PeerSelector = new PeerSelector(this.dateTimeProvider, this.loggerFactory, this.Peers);
        }

        /// <inheritdoc />
        public void LoadPeers()
        {
            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
            var peers = fileStorage.LoadByFileName(PeerFileName);
            peers.ForEach(peer =>
            {
                this.Peers.TryAdd(peer.Endpoint, peer);
            });

            ResetBannedPeers();
        }

        /// <inheritdoc />
        public void SavePeers()
        {
            if (this.Peers.Any() == false)
                return;

            var fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
            fileStorage.SaveToFile(this.Peers.OrderByDescending(p => p.Value.LastConnectionSuccess).Select(p => p.Value).ToList(), PeerFileName);
        }

        /// <inheritdoc/>
        public void AddPeer(IPEndPoint endPoint, IPAddress source)
        {
            if (!endPoint.Address.IsRoutable(true))
                return;

            var peerToAdd = PeerAddress.Create(endPoint, source);
            this.Peers.TryAdd(peerToAdd.Endpoint, peerToAdd);
        }

        /// <inheritdoc/>
        public void AddPeers(IPEndPoint[] endPoints, IPAddress source)
        {
            foreach (var endPoint in endPoints)
            {
                this.AddPeer(endPoint, source);
            }
        }

        /// <inheritdoc/>
        public void PeerAttempted(IPEndPoint endpoint, DateTime peerAttemptedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            //Reset the attempted count if:
            //1: The last attempt was more than the threshold time ago.
            //2: More than the threshold attempts was made.
            if (peer.Attempted &&
                peer.LastAttempt < this.dateTimeProvider.GetUtcNow().AddHours(-PeerAddress.AttemptResetThresholdHours) &&
                peer.ConnectionAttempts >= PeerAddress.AttemptThreshold)
            {
                peer.ResetAttempts();
            }

            peer.SetAttempted(peerAttemptedAt);
        }

        /// <inheritdoc/>
        public void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerConnectedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetConnected(peerConnectedAt);
        }

        /// <inheritdoc/>
        public void PeerDiscoveredFrom(IPEndPoint endpoint, DateTime peerDiscoveredFrom)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetDiscoveredFrom(peerDiscoveredFrom);
        }

        /// <inheritdoc/>
        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetHandshaked(peerHandshakedAt);
        }

        /// <inheritdoc/>
        public void PeerSeen(IPEndPoint endpoint, DateTime peerSeenAt)
        {
            var peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetLastSeen(peerSeenAt);
        }

        /// <inheritdoc/>
        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            var peer = this.Peers.Skip(0).SingleOrDefault(p => p.Key.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SavePeers();
        }

        /// <summary>
        /// When the PeerAddressManager saves peers to disk, check if any of
        /// banned Peers have expired.  If so, then reset the Peers.
        /// </summary>
        private void ResetBannedPeers()
        {
            foreach (KeyValuePair<IPEndPoint, PeerAddress> peer in 
                this.Peers.Where(p => p.Value.IsBanned.HasValue && p.Value.IsBanned.Value))
            {
                if (peer.Value.BanUntil < this.dateTimeProvider.GetUtcNow())
                {
                    peer.Value.IsBanned = false;
                    peer.Value.BanUntil = null;
                    peer.Value.BannedReason = string.Empty;
                }

                this.logger.LogTrace("({0}:'{1}' : No longer banned.)", nameof(peer.Value.Endpoint), peer.Value.Endpoint);
            }
        }
    }
}