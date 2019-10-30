using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Contract for <see cref="PeerDiscovery"/>.
    /// </summary>
    public interface IPeerDiscovery : IDisposable
    {
        /// <summary>
        /// Starts the peer discovery process.
        /// </summary>
        void DiscoverPeers(IConnectionManager connectionManager);
    }

    /// <summary>Async loop that discovers new peers to connect to.</summary>
    public sealed class PeerDiscovery : IPeerDiscovery
    {
        /// <summary>The async loop for performing discovery on actual peers. We need to wait upon it before we can shut down this connector.</summary>
        private IAsyncLoop discoverFromPeersLoop;

        /// <summary>The async loop for discovering from DNS seeds & seed nodes. We need to wait upon it before we can shut down this connector.</summary>
        private IAsyncLoop discoverFromDnsSeedsLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncProvider asyncProvider;

        /// <summary>The parameters cloned from the connection manager.</summary>
        private NetworkPeerConnectionParameters currentParameters;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>User defined node settings.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>The network the node is running on.</summary>
        private readonly Network network;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        private const int TargetAmountOfPeersToDiscover = 2000;

        public PeerDiscovery(
            IAsyncProvider asyncProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager)
        {
            this.asyncProvider = asyncProvider;
            this.loggerFactory = loggerFactory;
            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName);
            this.peerAddressManager = peerAddressManager;
            this.network = network;
            this.networkPeerFactory = networkPeerFactory;
            this.nodeLifetime = nodeLifetime;
            this.nodeSettings = nodeSettings;
        }

        /// <inheritdoc/>
        public void DiscoverPeers(IConnectionManager connectionManager)
        {
            // If peers are specified in the -connect arg then discovery does not happen.
            if (connectionManager.ConnectionSettings.Connect.Any())
                return;

            if (!connectionManager.Parameters.PeerAddressManagerBehaviour().Mode.HasFlag(PeerAddressManagerBehaviourMode.Discover))
                return;

            this.currentParameters = connectionManager.Parameters.Clone(); // TODO we shouldn't add all the behaviors, only those that we need.

            this.discoverFromDnsSeedsLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(this.DiscoverFromDnsSeedsAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < TargetAmountOfPeersToDiscover)
                    await this.DiscoverFromDnsSeedsAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpan.FromHours(1));

            this.discoverFromPeersLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(this.DiscoverPeersAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < TargetAmountOfPeersToDiscover)
                    await this.DiscoverPeersAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.TenSeconds);
        }

        /// <summary>
        /// See <see cref="DiscoverPeers"/>. This loop deals with discovery from DNS seeds and seed nodes as opposed to peers.
        /// </summary>
        private async Task DiscoverFromDnsSeedsAsync()
        {
            var peersToDiscover = new List<IPEndPoint>();

            // First see if we need to do DNS discovery at all. We may have peers from a previous cycle that still need to be tried.
            if (this.peerAddressManager.Peers.Select(a => !a.Attempted).Any())
            {
                this.logger.LogTrace("(-)[SKIP_DISCOVERY_UNATTEMPTED_PEERS_REMAINING]");
                return;
            }

            // At this point there are either no peers that we know of, or all the ones we do know of have been attempted & failed.
            this.AddDNSSeedNodes(peersToDiscover);
            this.AddSeedNodes(peersToDiscover);

            if (peersToDiscover.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_DNS_SEED_ADDRESSES]");
                return;
            }

            // Randomise the order prior to attempting connections.
            peersToDiscover = peersToDiscover.OrderBy(a => RandomUtils.GetInt32()).ToList();

            await this.ConnectToDiscoveryCandidatesAsync(peersToDiscover).ConfigureAwait(false);
        }

        /// <summary>
        /// See <see cref="DiscoverPeers"/>. This loop deals with discovery from peers as opposed to DNS seeds and seed nodes.
        /// </summary>
        private async Task DiscoverPeersAsync()
        {
            var peersToDiscover = new List<IPEndPoint>();

            // The peer selector returns a quantity of peers for discovery already in random order.
            List<PeerAddress> foundPeers = this.peerAddressManager.PeerSelector.SelectPeersForDiscovery(1000).ToList();
            peersToDiscover.AddRange(foundPeers.Select(p => p.Endpoint));

            if (peersToDiscover.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_ADDRESSES]");
                return;
            }

            await this.ConnectToDiscoveryCandidatesAsync(peersToDiscover).ConfigureAwait(false);
        }

        private async Task ConnectToDiscoveryCandidatesAsync(List<IPEndPoint> peersToDiscover)
        {
            await peersToDiscover.ForEachAsync(5, this.nodeLifetime.ApplicationStopping, async (endPoint, cancellation) =>
            {
                using (CancellationTokenSource connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    this.logger.LogDebug("Attempting to discover from : '{0}'", endPoint);

                    connectTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    INetworkPeer networkPeer = null;

                    // Try to connect to a peer with only the address-sharing behaviour, to learn about their peers and disconnect within 5 seconds.

                    try
                    {
                        NetworkPeerConnectionParameters clonedParameters = this.currentParameters.Clone();
                        clonedParameters.ConnectCancellation = connectTokenSource.Token;

                        PeerAddressManagerBehaviour addressManagerBehaviour = clonedParameters.TemplateBehaviors.OfType<PeerAddressManagerBehaviour>().FirstOrDefault();
                        clonedParameters.TemplateBehaviors.Clear();
                        clonedParameters.TemplateBehaviors.Add(addressManagerBehaviour);

                        networkPeer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(endPoint, clonedParameters).ConfigureAwait(false);
                        await networkPeer.VersionHandshakeAsync(connectTokenSource.Token).ConfigureAwait(false);

                        this.peerAddressManager.PeerDiscoveredFrom(endPoint, DateTimeProvider.Default.GetUtcNow());

                        connectTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                    }
                    finally
                    {
                        networkPeer?.Disconnect("Discovery job done");
                        networkPeer?.Dispose();
                    }

                    this.logger.LogDebug("Discovery from '{0}' finished", endPoint);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Add peers to the address manager from the network's DNS seed nodes.
        /// </summary>
        private void AddDNSSeedNodes(List<IPEndPoint> endPoints)
        {
            foreach (DNSSeedData seed in this.network.DNSSeeds)
            {
                try
                {
                    // We want to try to ensure we get a fresh set of results from the seeder each time we query it.
                    IPAddress[] ipAddresses = seed.GetAddressNodes(true);
                    endPoints.AddRange(ipAddresses.Select(ip => new IPEndPoint(ip, this.network.DefaultPort)));
                }
                catch (Exception)
                {
                    this.logger.LogWarning("Error getting seed node addresses from {0}.", seed.Host);
                }
            }
        }

        /// <summary>
        /// Add peers to the address manager from the network's seed nodes.
        /// </summary>
        private void AddSeedNodes(List<IPEndPoint> endPoints)
        {
            endPoints.AddRange(this.network.SeedNodes.Select(ipAddress => ipAddress.Endpoint));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.discoverFromPeersLoop?.Dispose();
            this.discoverFromDnsSeedsLoop?.Dispose();
        }
    }
}