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
    public sealed class PeerDiscoveryLoop : IDisposable
    {
        private IAsyncLoop asyncLoop;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly PeerAddressManager addressManager;
        private readonly CancellationToken applicationStopping;
        private readonly Network network;
        private readonly NodeConnectionParameters parameters;
        private readonly int peersToFind;

        public PeerDiscoveryLoop(Network network, IAsyncLoopFactory asyncLoopFactory, NodeConnectionParameters parameters, CancellationToken applicationStopping)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.applicationStopping = applicationStopping;
            this.asyncLoopFactory = asyncLoopFactory;
            this.parameters = parameters;
            this.addressManager = parameters.PeerAddressManager();
            this.network = network;
            this.peersToFind = this.parameters.PeerAddressManagerBehaviour().PeersToDiscover;
        }

        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.DiscoverPeersAsync), async token =>
            {
                await DiscoverPeersAsync().ConfigureAwait(false);
            },
            this.applicationStopping,
            TimeSpans.RunOnce);
        }

        private async Task DiscoverPeersAsync()
        {
            while (this.addressManager.Peers.Count < this.peersToFind)
            {
                this.applicationStopping.ThrowIfCancellationRequested();

                var peersToDiscover = new List<NetworkAddress>();
                peersToDiscover.AddRange(this.addressManager.SelectPeersToConnectTo());

                if (peersToDiscover.Count == 0)
                {
                    PopulateTableWithDNSNodes(peersToDiscover);
                    PopulateTableWithHardNodes(peersToDiscover);

                    peersToDiscover = new List<NetworkAddress>(peersToDiscover.OrderBy(a => RandomUtils.GetInt32()));
                    if (peersToDiscover.Count == 0)
                        await Task.CompletedTask;
                }

                var clonedParameters = this.parameters.Clone();

                Parallel.ForEach(peersToDiscover, new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 5,
                    CancellationToken = this.applicationStopping,
                },
                peer =>
                {
                    using (var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.applicationStopping))
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

                            connectTokenSource.Token.WaitHandle.WaitOne(2000);
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
            }

            await Task.CompletedTask;
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