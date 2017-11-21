using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Async loop that discovers new peers to connect to.
    /// </summary>
    public sealed class PeerDiscoveryLoop : IDisposable
    {
        /// <summary> The async loop we need to wait upon before we can shut down this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary> Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly NodeConnectionParameters parameters;

        /// <summary> Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        private readonly int peersToFind;
        private readonly Network network;

        public PeerDiscoveryLoop(
            IAsyncLoopFactory asyncLoopFactory,
            Network network,
            NodeConnectionParameters nodeConnectionParameters,
            INodeLifetime nodeLifetime,
            IPeerAddressManager peerAddressManager)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.asyncLoopFactory = asyncLoopFactory;
            this.parameters = nodeConnectionParameters;
            this.peerAddressManager = peerAddressManager;
            this.peersToFind = this.parameters.PeerAddressManagerBehaviour().PeersToDiscover;
            this.network = network;
            this.nodeLifetime = nodeLifetime;
        }

        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.StartAsync), async token =>
            {
                if (this.peerAddressManager.Peers.Count < this.peersToFind)
                    await this.StartAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.Second);
        }

        private Task StartAsync()
        {
            var peersToDiscover = new List<NetworkAddress>();
            peersToDiscover.AddRange(this.peerAddressManager.SelectPeersToConnectTo());

            if (peersToDiscover.Count == 0)
            {
                PopulateTableWithDNSNodes(peersToDiscover);
                PopulateTableWithHardNodes(peersToDiscover);

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

                    Node node = null;

                    try
                    {
                        clonedParameters.ConnectCancellation = connectTokenSource.Token;

                        var addressManagerBehaviour = clonedParameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
                        clonedParameters.TemplateBehaviors.Clear();
                        clonedParameters.TemplateBehaviors.Add(addressManagerBehaviour);

                        node = Node.Connect(this.network, peer.Endpoint, clonedParameters);
                        node.VersionHandshake(connectTokenSource.Token);
                        node.SendMessageAsync(new GetAddrPayload());

                        connectTokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                    }
                    finally
                    {
                        node?.DisconnectAsync();
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

        public void Dispose()
        {
            this.asyncLoop?.Dispose();
        }
    }
}