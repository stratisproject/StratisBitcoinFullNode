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
        public void LoadPeers()
        {
            List<PeerAddress> loadedPeers;

            try
            {
                loadedPeers = this.fileStorage.LoadByFileName(PeerFileName);
            }
            catch (Exception e)
            {
                this.logger.LogError("Error loading peers JSON, defaulting to empty list: {0}", e);

                loadedPeers = new List<PeerAddress>();
            }

            if (loadedPeers == null)
                loadedPeers = new List<PeerAddress>();

            // If the loaded peers is still empty at this point, we rely on the discovery loop to find new peers to connect to.
            // The discovery loop will only not run if -connect is used or if there are un-attempted peers left, so as long as
            // there are seed nodes & DNS seeds available we should get something to connect to relatively rapidly.

            this.logger.LogDebug("{0} peers were loaded.", loadedPeers.Count);

            foreach (PeerAddress peer in loadedPeers)
            {
                // If no longer banned reset ban details.
                if (peer.BanUntil.HasValue && peer.BanUntil < this.dateTimeProvider.GetUtcNow())
                {
                    peer.UnBan();

                    this.logger.LogDebug("{0} no longer banned.", peer.Endpoint);
                }

                // Reset the peer if the attempt threshold has been reached and the attempt window has lapsed.
                if (peer.CanResetAttempts)
                    peer.ResetAttempts();

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
        public PeerAddress AddPeer(IPEndPoint endPoint, IPAddress source)
        {
            PeerAddress peerAddress = this.AddPeerWithoutCleanup(endPoint, source);

            this.EnsureMaxItemsPerSource(source);

            return peerAddress;
        }

        private PeerAddress AddPeerWithoutCleanup(IPEndPoint endPoint, IPAddress source)
        {
            if (!endPoint.Address.IsRoutable(true))
            {
                this.logger.LogTrace("(-)[PEER_NOT_ADDED_ISROUTABLE]:{0}", endPoint);
                return null;
            }

            IPEndPoint ipv6EndPoint = endPoint.MapToIpv6();

            PeerAddress peerToAdd = PeerAddress.Create(ipv6EndPoint, source.MapToIPv6());
            var added = this.peerInfoByPeerAddress.TryAdd(ipv6EndPoint, peerToAdd);
            if (added)
            {
                this.logger.LogTrace("(-)[PEER_ADDED]:{0}", endPoint);
                return peerToAdd;
            }

            this.logger.LogTrace("(-)[PEER_NOT_ADDED_ALREADY_EXISTS]:{0}", endPoint);
            return null;
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

            if (peer.CanResetAttempts)
                peer.ResetAttempts();

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