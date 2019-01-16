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

        /// <summary>Key value store that indexes all discovered peers by their end point.</summary>
        private ConcurrentDictionary<IPEndPoint, PeerAddress> peerInfoByPeerAddress;

        /// <inheritdoc />
        public ICollection<PeerAddress> Peers => this.peerInfoByPeerAddress.Values;

        /// <summary>The file name of the peers file.</summary>
        internal const string PeerFileName = "peers.json";

        /// <inheritdoc />
        public DataFolder PeerFilePath { get; set; }

        /// <summary>Peer selector instance, used to select peers to connect to.</summary>
        public IPeerSelector PeerSelector { get; private set; }

        /// <summary>An object capable of storing a list of <see cref="PeerAddress"/>s to the file system.</summary>
        private readonly FileStorage<List<PeerAddress>> fileStorage;

        private const int MaxAddressesToStoreFromSingleIp = 1500;

        /// <summary>Constructor used by dependency injection.</summary>
        public PeerAddressManager(IDateTimeProvider dateTimeProvider, DataFolder peerFilePath, ILoggerFactory loggerFactory, ISelfEndpointTracker selfEndpointTracker)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerInfoByPeerAddress = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            this.PeerFilePath = peerFilePath;
            this.PeerSelector = new PeerSelector(this.dateTimeProvider, loggerFactory, this.peerInfoByPeerAddress, selfEndpointTracker);
            this.fileStorage = new FileStorage<List<PeerAddress>>(this.PeerFilePath.AddressManagerFilePath);
        }

        /// <inheritdoc />
        [NoTrace]
        public void LoadPeers()
        {
            List<PeerAddress> loadedPeers = this.fileStorage.LoadByFileName(PeerFileName);

            this.logger.LogTrace("{0} peers were loaded.", loadedPeers.Count);

            foreach (PeerAddress peer in loadedPeers)
            {
                // If no longer banned reset ban details.
                if (peer.BanUntil.HasValue && peer.BanUntil < this.dateTimeProvider.GetUtcNow())
                {
                    peer.BanTimeStamp = null;
                    peer.BanUntil = null;
                    peer.BanReason = string.Empty;

                    this.logger.LogTrace("{0} no longer banned.", peer.Endpoint);
                }

                this.peerInfoByPeerAddress.TryAdd(peer.Endpoint, peer);
            }
        }

        /// <inheritdoc />
        public void SavePeers()
        {
            if (!this.peerInfoByPeerAddress.Any())
                return;

            this.fileStorage.SaveToFile(this.peerInfoByPeerAddress.Values.ToList(), PeerFileName);
        }

        /// <inheritdoc/>
        public void AddPeer(IPEndPoint endPoint, IPAddress source)
        {
            this.AddPeerWithoutCleanup(endPoint, source);

            this.EnsureMaxItemsPerSource(source);
        }

        private void AddPeerWithoutCleanup(IPEndPoint endPoint, IPAddress source)
        {
            if (!endPoint.Address.IsRoutable(true))
                return;

            IPEndPoint ipv6EndPoint = endPoint.MapToIpv6();

            PeerAddress peerToAdd = PeerAddress.Create(ipv6EndPoint, source.MapToIPv6());
            this.peerInfoByPeerAddress.TryAdd(ipv6EndPoint, peerToAdd);
        }

        /// <inheritdoc/>
        public void AddPeers(IEnumerable<IPEndPoint> endPoints, IPAddress source)
        {
            foreach (IPEndPoint endPoint in endPoints)
                this.AddPeerWithoutCleanup(endPoint, source);

            this.EnsureMaxItemsPerSource(source);
        }

        private void EnsureMaxItemsPerSource(IPAddress source)
        {
            IEnumerable<IPEndPoint> itemsFromSameSource = this.peerInfoByPeerAddress.Values.Where(x => x.Loopback.Equals(source.MapToIPv6())).Select(x => x.Endpoint);
            List<IPEndPoint> itemsToRemove = itemsFromSameSource.Skip(MaxAddressesToStoreFromSingleIp).ToList();

            if (itemsToRemove.Count > 0)
            {
                foreach (IPEndPoint toRemove in itemsToRemove)
                    this.RemovePeer(toRemove);
            }
        }

        /// <inheritdoc/>
        public void RemovePeer(IPEndPoint endPoint)
        {
            this.peerInfoByPeerAddress.TryRemove(endPoint.MapToIpv6(), out PeerAddress addr);
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

            peer?.SetConnected(peerConnectedAt);
        }

        /// <inheritdoc/>
        public void PeerDiscoveredFrom(IPEndPoint endpoint, DateTime peerDiscoveredFrom)
        {
            PeerAddress peer = this.FindPeer(endpoint);

            peer?.SetDiscoveredFrom(peerDiscoveredFrom);
        }

        /// <inheritdoc/>
        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            PeerAddress peer = this.FindPeer(endpoint);

            peer?.SetHandshaked(peerHandshakedAt);
        }

        /// <inheritdoc/>
        public void PeerSeen(IPEndPoint endpoint, DateTime peerSeenAt)
        {
            PeerAddress peer = this.FindPeer(endpoint);

            peer?.SetLastSeen(peerSeenAt);
        }

        /// <inheritdoc/>
        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            KeyValuePair<IPEndPoint, PeerAddress> peer = this.peerInfoByPeerAddress.Skip(0).SingleOrDefault(p => p.Key.Match(endPoint));
            return peer.Value;
        }

        /// <inheritdoc/>
        public List<PeerAddress> FindPeersByIp(IPEndPoint endPoint)
        {
            IEnumerable<KeyValuePair<IPEndPoint, PeerAddress>> peers = this.peerInfoByPeerAddress.Skip(0).Where(p => p.Key.MatchIpOnly(endPoint));
            return peers.Select(p => p.Value).ToList();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.SavePeers();
        }
    }
}