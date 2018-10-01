using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P
{
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

        /// <summary>Key value store that indexes all discovered peers by their end point.</summary>
        private readonly ConcurrentDictionary<IPEndPoint, PeerAddress> peers;

        /// <inheritdoc />
        public List<PeerAddress> Peers => this.peers.Select(item => item.Value).ToList();

        /// <summary>The file name of the peers file.</summary>
        internal const string PeerFileName = "peers.json";

        /// <inheritdoc />
        public DataFolder PeerFilePath { get; set; }

        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        public IPeerSelector PeerSelector { get; private set; }

        /// <summary>An object capable of storing a list of <see cref="PeerAddress"/>s to the file system.</summary>
        private readonly FileStorage<List<PeerAddress>> fileStorage;

        /// <summary>Constructor used by dependency injection.</summary>
        public PeerAddressManager(IDateTimeProvider dateTimeProvider, DataFolder peerFilePath, ILoggerFactory loggerFactory, ISelfEndpointTracker selfEndpointTracker)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peers = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            this.PeerFilePath = peerFilePath;
            this.PeerSelector = new PeerSelector(this.dateTimeProvider, this.loggerFactory, this.peers, selfEndpointTracker);
            this.fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
        }

        /// <inheritdoc />
        [NoTrace]
        public void LoadPeers()
        {
            List<PeerAddress> loadedPeers = this.fileStorage.LoadByFileName(PeerFileName);
            this.logger.LogTrace("{0} peers were loaded.", loadedPeers.Count);

            loadedPeers.ForEach(peer =>
            {
                // Ensure that any address already in store is mapped.
                peer.Endpoint = peer.Endpoint.MapToIpv6();

                // If no longer banned reset ban details.
                if (peer.BanUntil.HasValue && peer.BanUntil < this.dateTimeProvider.GetUtcNow())
                {
                    peer.BanTimeStamp = null;
                    peer.BanUntil = null;
                    peer.BanReason = string.Empty;

                    this.logger.LogTrace("{0} no longer banned.", peer.Endpoint);
                }

                this.peers.AddOrUpdate(peer.Endpoint, peer, (key, oldValue) => peer);
            });
        }

        /// <inheritdoc />
        public void SavePeers()
        {
            if (this.peers.Any() == false)
                return;

            ICollection<PeerAddress> snapshotOfPeersToSave = this.peers.Values;
            this.fileStorage.SaveToFile(
                snapshotOfPeersToSave
                    .OrderByDescending(p => p.LastConnectionSuccess)
                    .ToList(),
                PeerFileName);
        }

        /// <inheritdoc/>
        public void AddPeer(IPEndPoint endPoint, IPAddress source)
        {
            if (!endPoint.Address.IsRoutable(true))
                return;

            PeerAddress peerToAdd = PeerAddress.Create(endPoint, source);
            this.peers.TryAdd(peerToAdd.Endpoint, peerToAdd);
        }

        /// <inheritdoc/>
        public void RemovePeer(IPEndPoint endPoint)
        {
           this.peers.TryRemove(endPoint.MapToIpv6(), out PeerAddress address);
        }

        /// <inheritdoc/>
        public void AddPeers(IPEndPoint[] endPoints, IPAddress source)
        {
            foreach (IPEndPoint endPoint in endPoints)
            {
                this.AddPeer(endPoint, source);
            }
        }

        /// <inheritdoc/>
        public void PeerAttempted(IPEndPoint endpoint, DateTime peerAttemptedAt)
        {
            PeerAddress peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            // Reset the attempted count if:
            // 1: The last attempt was more than the threshold time ago.
            // 2: More than the threshold attempts was made.
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
            PeerAddress peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetConnected(peerConnectedAt);
        }

        /// <inheritdoc/>
        public void PeerDiscoveredFrom(IPEndPoint endpoint, DateTime peerDiscoveredFrom)
        {
            PeerAddress peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetDiscoveredFrom(peerDiscoveredFrom);
        }

        /// <inheritdoc/>
        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            PeerAddress peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetHandshaked(peerHandshakedAt);
        }

        /// <inheritdoc/>
        public void PeerSeen(IPEndPoint endpoint, DateTime peerSeenAt)
        {
            PeerAddress peer = this.FindPeer(endpoint);
            if (peer == null)
                return;

            peer.SetLastSeen(peerSeenAt);
        }

        /// <inheritdoc/>
        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            KeyValuePair<IPEndPoint, PeerAddress> peer = this.peers.Skip(0).SingleOrDefault(p => p.Key.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SavePeers();
        }
    }
}