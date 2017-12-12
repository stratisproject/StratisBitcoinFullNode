using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

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
        /// <param name="parentParameters">The parent parameters as injected by <see cref="Connection.ConnectionManager"/>.</param>
        void DiscoverPeers(NetworkPeerConnectionParameters parentParameters);
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
        public void DiscoverPeers(NetworkPeerConnectionParameters parameters)
        {
            // If peers are specified in the -connect arg then discovery does not happen.
            if (this.nodeSettings.ConnectionManager.Connect.Any())
                return;

            if (!parameters.PeerAddressManagerBehaviour().Mode.HasFlag(PeerAddressManagerBehaviourMode.Discover))
                return;

            this.currentParameters = parameters;
            this.peersToFind = this.currentParameters.PeerAddressManagerBehaviour().PeersToDiscover;

            this.logger.LogInformation("Starting peer discovery...");
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.DiscoverPeersAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < this.peersToFind)
                    await this.DiscoverPeersAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.Minute);
        }

        /// <summary>
        /// See <see cref="DiscoverPeers"/>
        /// </summary>
        private Task DiscoverPeersAsync()
        {
            var peersToDiscover = new List<NetworkAddress>();
            peersToDiscover.AddRange(this.peerAddressManager.SelectPeersToConnectTo());

            if (peersToDiscover.Count == 0)
            {
                this.AddDNSSeedNodes(peersToDiscover);
                this.AddSeedNodes(peersToDiscover);

                peersToDiscover = new List<NetworkAddress>(peersToDiscover.OrderBy(a => RandomUtils.GetInt32()));
                if (peersToDiscover.Count == 0)
                    return Task.CompletedTask;
            }

            var clonedParameters = this.currentParameters.Clone();

            Parallel.ForEach(peersToDiscover, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 5,
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
                        networkPeer?.DisconnectWithException();
                    }
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Add peers to the address manager from the network DNS's seed nodes.
        /// </summary>
        private void AddDNSSeedNodes(List<NetworkAddress> peers)
        {
            peers.AddRange(this.network.DNSSeeds.SelectMany(seed =>
            {
                try
                {
                    return seed.GetAddressNodes();
                }
                catch (Exception)
                {
                    return new IPAddress[0];
                }
            })
            .Select(d => new NetworkAddress(d, this.network.DefaultPort)));
        }

        /// <summary>
        /// Add peers to the address manager from the network's seed nodes.
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
