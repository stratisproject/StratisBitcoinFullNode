using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
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
        /// <summary>The async loop we need to wait upon before we can shut down this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

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

        /// <summary>Indicates the dns and seed nodes were attempted.</summary>
        private bool isSeedAndDnsAttempted;

        private const int TargetAmountOfPeersToDiscover = 2000;

        public PeerDiscovery(
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            IPeerAddressManager peerAddressManager)
        {
            this.asyncLoopFactory = asyncLoopFactory;
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

            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.DiscoverPeersAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < TargetAmountOfPeersToDiscover)
                    await this.DiscoverPeersAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.TenSeconds);
        }

        /// <summary>
        /// See <see cref="DiscoverPeers"/>.
        /// </summary>
        private async Task DiscoverPeersAsync()
        {
            var peersToDiscover = new List<IPEndPoint>();
            List<PeerAddress> foundPeers = this.peerAddressManager.PeerSelector.SelectPeersForDiscovery(1000).ToList();
            peersToDiscover.AddRange(foundPeers.Select(p => p.Endpoint));

            if (peersToDiscover.Count == 0)
            {
                // On normal circumstances the dns seeds are attempted only once per node lifetime.
                if (this.isSeedAndDnsAttempted)
                {
                    this.logger.LogTrace("(-)[DNS_ATTEMPTED]");
                    return;
                }

                this.AddDNSSeedNodes(peersToDiscover);
                this.AddSeedNodes(peersToDiscover);
                this.isSeedAndDnsAttempted = true;

                if (peersToDiscover.Count == 0)
                {
                    this.logger.LogTrace("(-)[NO_ADDRESSES]");
                    return;
                }

                peersToDiscover = peersToDiscover.OrderBy(a => RandomUtils.GetInt32()).ToList();
            }
            else
            {
                // If all attempts have failed then attempt the dns seeds again.
                if (!this.isSeedAndDnsAttempted && foundPeers.All(peer => peer.Attempted))
                {
                    peersToDiscover.Clear();
                    this.AddDNSSeedNodes(peersToDiscover);
                    this.AddSeedNodes(peersToDiscover);
                    this.isSeedAndDnsAttempted = true;
                }
            }

            await peersToDiscover.ForEachAsync(5, this.nodeLifetime.ApplicationStopping, async (endPoint, cancellation) =>
            {
                using (CancellationTokenSource connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    this.logger.LogTrace("Attempting to discover from : '{0}'", endPoint);

                    connectTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    INetworkPeer networkPeer = null;

                    try
                    {
                        NetworkPeerConnectionParameters clonedParameters = this.currentParameters.Clone();
                        clonedParameters.ConnectCancellation = connectTokenSource.Token;

                        PeerAddressManagerBehaviour addressManagerBehaviour = clonedParameters.TemplateBehaviors.OfType<PeerAddressManagerBehaviour>().FirstOrDefault();
                        clonedParameters.TemplateBehaviors.Clear();
                        clonedParameters.TemplateBehaviors.Add(addressManagerBehaviour);

                        networkPeer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(endPoint, clonedParameters).ConfigureAwait(false);
                        await networkPeer.VersionHandshakeAsync(connectTokenSource.Token).ConfigureAwait(false);
                        await networkPeer.SendMessageAsync(new GetAddrPayload(), connectTokenSource.Token).ConfigureAwait(false);

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

                    this.logger.LogTrace("Discovery from '{0}' finished", endPoint);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Add peers to the address manager from the network DNS's seed nodes.
        /// </summary>
        private void AddDNSSeedNodes(List<IPEndPoint> endPoints)
        {
            foreach (DNSSeedData seed in this.network.DNSSeeds)
            {
                try
                {
                    IPAddress[] ipAddresses = seed.GetAddressNodes();
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
            this.asyncLoop?.Dispose();
        }
    }
}