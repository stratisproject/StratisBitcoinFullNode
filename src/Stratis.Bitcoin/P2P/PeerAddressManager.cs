using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.FileStorage;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// The AddressManager keeps a set of peers discovered on the network in cache and on disk.
    /// <para>
    /// The manager updates their states according to how recent they have been connected to.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManager : IDisposable
    {
        private IAsyncLoop asyncLoop;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly string peerFilePath;
        internal const string PEERFILENAME = "peers.json";

        public PeerAddressManager(IAsyncLoopFactory asyncLoopFactory, string peerFilePath)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.asyncLoopFactory = asyncLoopFactory;
            this.peerFilePath = peerFilePath;
        }

        private ConcurrentDictionary<int, PeerAddress> peers = new ConcurrentDictionary<int, PeerAddress>();
        public ConcurrentDictionary<int, PeerAddress> Peers
        {
            get { return this.peers; }
        }

        public void LoadPeers()
        {
            var fileStorage = new FileStorage<List<PeerAddress>>(this.peerFilePath);
            var peers = fileStorage.LoadByFileName(PEERFILENAME);
            peers.ForEach(peer =>
            {
                this.peers.TryAdd(peer.NetworkAddress.Endpoint.GetHashCode(), peer);
            });
        }

        public void SavePeers()
        {
            if (this.peers.Any() == false)
                return;

            var fileStorage = new FileStorage<List<PeerAddress>>(this.peerFilePath);
            fileStorage.SaveToFile(this.peers.Select(p => p.Value).ToList(), PEERFILENAME);
        }

        public void AddPeer(NetworkAddress networkAddress, IPAddress source)
        {
            //[NBitcoin] what is this check?
            if (networkAddress.Endpoint.Address.IsRoutable(true) == false)
                return;

            var peerToAdd = PeerAddress.Create(networkAddress, source);
            this.peers.TryAdd(peerToAdd.NetworkAddress.Endpoint.GetHashCode(), peerToAdd);
        }

        public void AddPeer(NetworkAddress[] networkAddresses, IPAddress source)
        {
            foreach (var networkAddress in networkAddresses)
            {
                AddPeer(networkAddress, source);
            }
        }

        public void AddPeer(PeerAddress peer)
        {
            //[NBitcoin] what is this check?
            if (peer.NetworkAddress.Endpoint.Address.IsRoutable(true) == false)
                return;

            this.peers.TryAdd(peer.NetworkAddress.Endpoint.GetHashCode(), peer);
        }

        public void DeletePeer(PeerAddress peer)
        {
            IPEndPoint endPoint = null;
            this.peers.TryRemove(endPoint.GetHashCode(), out peer);
        }

        public void PeerAttempted(IPEndPoint endpoint, DateTimeOffset peerAttemptedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.Attempted(peerAttemptedAt);
        }

        public void PeerConnected(IPEndPoint endpoint, DateTimeOffset peerConnectedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.Connected(peerConnectedAt);
        }

        public void PeerHandshaked(IPEndPoint endpoint, DateTimeOffset peerHandshakedAt)
        {
            var peer = FindPeer(endpoint);
            if (peer == null)
                return;

            peer.Handshaked(peerHandshakedAt);
        }

        public PeerAddress FindPeer(IPEndPoint endPoint)
        {
            var peer = this.peers.SingleOrDefault(p => p.Value.Match(endPoint));
            if (peer.Value != null)
                return peer.Value;
            return null;
        }

        /// <summary>
        /// Selects a random peer to connect to.
        /// 
        /// [NBitcoin] Use a 50% chance for choosing between tried and new table entries.
        /// </summary>
        public NetworkAddress SelectPeerToConnectTo()
        {
            if (this.peers.Tried().Any() == true &&
                (this.peers.New().Any() == false || GetRandomInteger(2) == 0))
                return this.peers.Tried().Random().NetworkAddress;

            if (this.peers.New().Any() == true)
                return this.peers.New().Random().NetworkAddress;

            return null;
        }

        /// <summary>
        /// Selects a random set of preferred peers to connects to.
        /// </summary>
        public IEnumerable<NetworkAddress> SelectPeersToConnectTo()
        {
            return this.peers.Where(p => p.Value.Preferred).Select(p => p.Value.NetworkAddress);
        }

        internal static int GetRandomInteger(int max)
        {
            return (int)(RandomUtils.GetUInt32() % (uint)max);
        }

        internal void DiscoverPeers(Network network, NodeConnectionParameters nodeParameters, int peersToFind)
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.DiscoverPeersAsync), async token =>
            {
                await DiscoverPeersAsync(network, nodeParameters, peersToFind).ConfigureAwait(false);
            },
            nodeParameters.ConnectCancellation,
            TimeSpans.RunOnce);
        }

        private async Task DiscoverPeersAsync(Network network, NodeConnectionParameters nodeParameters, int peersToFind)
        {
            int peersFound = 0;

            while (peersFound < peersToFind)
            {
                nodeParameters.ConnectCancellation.ThrowIfCancellationRequested();

                var peersToDiscover = new List<NetworkAddress>();
                peersToDiscover.AddRange(SelectPeersToConnectTo());

                if (peersToDiscover.Count == 0)
                {
                    PopulateTableWithDNSNodes(network, peersToDiscover);
                    PopulateTableWithHardNodes(network, peersToDiscover);
                    peersToDiscover = new List<NetworkAddress>(peersToDiscover.OrderBy(a => RandomUtils.GetInt32()));
                    if (peersToDiscover.Count == 0)
                        await Task.CompletedTask;
                }

                try
                {
                    var clonedParameters = nodeParameters.Clone();

                    Parallel.ForEach(peersToDiscover, new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = 5,
                        CancellationToken = nodeParameters.ConnectCancellation,
                    },
                    peer =>
                    {
                        using (var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(nodeParameters.ConnectCancellation))
                        {
                            connectTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                            Node node = null;

                            try
                            {
                                clonedParameters.ConnectCancellation = connectTokenSource.Token;

                                var addressManagerBehaviour = clonedParameters.TemplateBehaviors.Find<PeerAddressManagerBehaviour>();
                                clonedParameters.TemplateBehaviors.Clear();
                                clonedParameters.TemplateBehaviors.Add(addressManagerBehaviour);

                                node = Node.Connect(network, peer.Endpoint, clonedParameters);
                                node.VersionHandshake(connectTokenSource.Token);
                                node.MessageReceived += (s, a) =>
                                {
                                    if (a.Message.Payload is AddrPayload addressPayload)
                                        Interlocked.Add(ref peersFound, addressPayload.Addresses.Length);
                                };

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
                catch (OperationCanceledException)
                {
                    if (nodeParameters.ConnectCancellation.IsCancellationRequested)
                        throw;
                }
            }

            await Task.CompletedTask;
        }

        private void PopulateTableWithDNSNodes(Network network, List<NetworkAddress> peers)
        {
            peers.AddRange(network.DNSSeeds.SelectMany(seed =>
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
            .Select(d => new NetworkAddress(d, network.DefaultPort)));
        }

        private void PopulateTableWithHardNodes(Network network, List<NetworkAddress> peers)
        {
            peers.AddRange(network.SeedNodes);
        }

        public void Dispose()
        {
            this.asyncLoop?.Dispose();

            SavePeers();
        }
    }

    public static class PeerAddressExtensions
    {
        public static IEnumerable<PeerAddress> New(this ConcurrentDictionary<int, PeerAddress> peers)
        {
            return peers.Where(p => p.Value.IsNew).Select(p => p.Value);
        }

        public static IEnumerable<PeerAddress> Tried(this ConcurrentDictionary<int, PeerAddress> peers)
        {
            return peers.Where(p => !p.Value.IsNew).Select(p => p.Value);
        }

        /// <summary>
        /// TODO: Do we need to use a chance factor, as in NBitcoin, to select a random peer?
        /// </summary>
        public static PeerAddress Random(this IEnumerable<PeerAddress> peers)
        {
            //var randomPeerIndex = PeerAddressManager.GetRandomInteger(peers.Count() - 1);
            //return peers.ToArray()[randomPeerIndex];

            //Using "Chance" from NBitcoin-----------------
            double chanceFactor = 1.0;
            while (true)
            {
                if (peers.Count() == 1)
                    return peers.ToArray()[0];

                var randomPeerIndex = PeerAddressManager.GetRandomInteger(peers.Count() - 1);
                var randomPeer = peers.ToArray()[randomPeerIndex];

                if (PeerAddressManager.GetRandomInteger(1 << 30) < chanceFactor * randomPeer.Selectability * (1 << 30))
                    return randomPeer;

                chanceFactor *= 1.2;
            }
            //---------------------------------------------
        }
    }
}