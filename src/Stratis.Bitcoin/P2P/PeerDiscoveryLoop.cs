using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Async loop that discovers new peers to connect to.</summary>
    public sealed class PeerDiscoveryLoop : IDisposable
    {
        /// <summary>The async loop we need to wait upon before we can shut down this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly NetworkPeerConnectionParameters parameters;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>The amount of peers to find.</summary>
        private readonly int peersToFind;

        /// <summary>The network the node is running on.</summary>
        private readonly Network network;

        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        public PeerDiscoveryLoop(
            IAsyncLoopFactory asyncLoopFactory,
            Network network,
            NetworkPeerConnectionParameters connectionParameters,
            INodeLifetime nodeLifetime,
            IPeerAddressManager peerAddressManager,
            INetworkPeerFactory networkPeerFactory)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.asyncLoopFactory = asyncLoopFactory;
            this.parameters = connectionParameters;
            this.peerAddressManager = peerAddressManager;
            this.peersToFind = this.parameters.PeerAddressManagerBehaviour().PeersToDiscover;
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.networkPeerFactory = networkPeerFactory;
        }

        /// <summary>
        /// Starts an asynchronous loop that periodicly tries to discover new peers to add to the
        /// <see cref="PeerAddressManager"/>.
        /// </summary>
        public void DiscoverPeers()
        {
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
                this.PopulateTableWithDNSNodes(peersToDiscover);
                this.PopulateTableWithHardNodes(peersToDiscover);

                peersToDiscover = new List<NetworkAddress>(peersToDiscover.OrderBy(a => RandomUtils.GetInt32()));
                if (peersToDiscover.Count == 0)
                    return Task.CompletedTask;
            }

            var clonedParameters = this.parameters.Clone();

            Parallel.ForEach(peersToDiscover, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 5,
                CancellationToken = this.nodeLifetime.ApplicationStopping,
            },
            peer =>
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

                        networkPeer = this.networkPeerFactory.CreateConnectedNetworkPeer(this.network, peer.Endpoint, clonedParameters);
                        networkPeer.VersionHandshake(connectTokenSource.Token);
                        networkPeer.SendMessageAsync(new GetAddrPayload());

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

        private void PopulateTableWithDNSNodes(List<NetworkAddress> peers)
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

        private void PopulateTableWithHardNodes(List<NetworkAddress> peers)
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
