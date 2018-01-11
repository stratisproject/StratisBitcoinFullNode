using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
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

        /// <summary>The amount of peers to find.</summary>
        private int peersToFind;

        /// <summary>The network the node is running on.</summary>
        private readonly Network network;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

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
            if (this.nodeSettings.ConnectionManager.Connect.Any())
                return;

            if (!connectionManager.Parameters.PeerAddressManagerBehaviour().Mode.HasFlag(PeerAddressManagerBehaviourMode.Discover))
                return;

            this.currentParameters = connectionManager.Parameters.Clone();
            this.currentParameters.TemplateBehaviors.Add(new ConnectionManagerBehavior(false, connectionManager, this.loggerFactory));

            this.peersToFind = this.currentParameters.PeerAddressManagerBehaviour().PeersToDiscover;

            this.logger.LogInformation("Starting peer discovery...");
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.DiscoverPeersAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < this.peersToFind)
                    await this.DiscoverPeersAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.TenSeconds);
        }

        /// <summary>
        /// See <see cref="DiscoverPeers"/>
        /// </summary>
        private Task DiscoverPeersAsync()
        {
            var peersToDiscover = new List<NetworkAddress>();

            //First add peers for discovery from the current set of peers
            if (this.peerAddressManager.Peers.Any())
                peersToDiscover.AddRange(this.peerAddressManager.PeerSelector.SelectPeers(1000).Select(p => p.NetworkAddress));

            //If none exists add peers from the DNS seed nodes
            if (!peersToDiscover.Any())
                this.AddDNSSeedNodes(peersToDiscover);

            //If none exists add peers from the hard-coded IP seed nodes
            if (!peersToDiscover.Any())
                this.AddSeedNodes(peersToDiscover);

            //If none exists return
            if (!peersToDiscover.Any())
                return Task.CompletedTask;

            Parallel.ForEach(peersToDiscover, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 2,
                CancellationToken = this.nodeLifetime.ApplicationStopping,
            },
            async peer =>
            {
                using (var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    connectTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                    NetworkPeer networkPeer = null;

                    try
                    {
                        NetworkPeerConnectionParameters clonedParameters = this.currentParameters.Clone();
                        clonedParameters.ConnectCancellation = connectTokenSource.Token;

                        var addressManagerBehaviour = clonedParameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
                        clonedParameters.TemplateBehaviors.Clear();
                        clonedParameters.TemplateBehaviors.Add(addressManagerBehaviour);

                        networkPeer = await this.networkPeerFactory.CreateConnectedNetworkPeerAsync(this.network, peer.Endpoint, clonedParameters).ConfigureAwait(false);
                        await networkPeer.VersionHandshakeAsync(connectTokenSource.Token).ConfigureAwait(false);
                        await networkPeer.SendMessageAsync(new GetAddrPayload(), connectTokenSource.Token).ConfigureAwait(false);

                        connectTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                    }
                    finally
                    {
                        networkPeer?.Dispose("Discovery job done");
                    }
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Add peers to discover from the network DNS seed nodes.
        /// </summary>
        private void AddDNSSeedNodes(List<NetworkAddress> peers)
        {
            foreach (var seed in this.network.DNSSeeds)
            {
                try
                {
                    var seedAddresses = seed.GetAddressNodes().Select(ip => new NetworkAddress(ip, this.network.DefaultPort));
                    peers.AddRange(seedAddresses);
                }
                catch (Exception exception)
                {
                    this.logger.LogTrace("Address retrieval from seed {0} failed, error: {1}" + exception.Message);
                }
            }
        }

        /// <summary>
        /// Add peers to discover from the network's hard coded IP seed nodes.
        /// </summary>
        private void AddSeedNodes(List<NetworkAddress> peers)
        {
            peers.AddRange(this.network.SeedNodes);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}
