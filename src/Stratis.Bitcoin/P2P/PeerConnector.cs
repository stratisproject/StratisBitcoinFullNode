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
    public sealed class WellKnownGroupSelectors
    {
        private static Func<IPEndPoint, byte[]> byEndpoint;
        public static Func<IPEndPoint, byte[]> ByEndpoint
        {
            get
            {
                return byEndpoint = byEndpoint ?? new Func<IPEndPoint, byte[]>((endpoint) =>
                {
                    var bytes = endpoint.Address.GetAddressBytes();
                    var port = Utils.ToBytes((uint)endpoint.Port, true);
                    var result = new byte[bytes.Length + port.Length];
                    Array.Copy(bytes, result, bytes.Length);
                    Array.Copy(port, 0, result, bytes.Length, port.Length);
                    return bytes;
                });
            }
        }

        private static Func<IPEndPoint, byte[]> byNetwork;
        public static Func<IPEndPoint, byte[]> ByNetwork
        {
            get
            {
                return byNetwork = byNetwork ?? new Func<IPEndPoint, byte[]>((ip) =>
                {
                    return IpExtensions.GetGroup(ip.Address);
                });
            }
        }
    }

    public sealed class RelatedNodeGroups : Dictionary<string, PeerConnector>
    {
        public void Register(string name, PeerConnector nodeGroup)
        {
            if (nodeGroup != null)
            {
                this.Add(name, nodeGroup);
                nodeGroup.RelatedGroups = this;
            }
        }

        public IPEndPoint[] GlobalConnectedNodes()
        {
            IPEndPoint[] all = new IPEndPoint[0];
            foreach (var kv in this)
            {
                var endPoints = kv.Value.ConnectedNodes.Select(n => n.RemoteSocketEndpoint).ToArray<IPEndPoint>();
                all = all.Union<IPEndPoint>(endPoints).ToArray<IPEndPoint>();
            }

            return all;
        }
    }

    public sealed class PeerConnector : IDisposable
    {
        internal PeerConnector(Network network, INodeLifetime nodeLifeTime, NodeConnectionParameters parameters, NodeRequirement nodeRequirements, Func<IPEndPoint, byte[]> groupSelector, IAsyncLoopFactory asyncLoopFactory)
        {
            this.disconnect = CancellationTokenSource.CreateLinkedTokenSource(nodeLifeTime.ApplicationStopping);
            this.MaximumNodeConnections = 8;

            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedNodes = new NodesCollection();
            this.groupSelector = groupSelector;
            this.network = network;
            this.ParentParameters = parameters;
            this.Requirements = nodeRequirements;

            this.currentParameters = this.ParentParameters.Clone();
            this.currentParameters.TemplateBehaviors.Add(new NodeGroupBehavior(this));
            this.currentParameters.ConnectCancellation = this.disconnect.Token;
        }

        /// <summary> The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary> Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private int busyConnectingToPeers;
        internal NodesCollection ConnectedNodes { get; private set; }

        /// <summary> The cloned parameters used to connect to peers. </summary>
        private NodeConnectionParameters currentParameters;

        private CancellationTokenSource disconnect;

        /// <summary> How to calculate a group of an IP, by default using NBitcoin.IpExtensions.GetGroup.</summary>
        private Func<IPEndPoint, byte[]> groupSelector;

        internal int MaximumNodeConnections { get; set; }
        private Network network;
        internal NodeConnectionParameters ParentParameters { get; private set; }
        internal RelatedNodeGroups RelatedGroups { get; set; }
        internal NodeRequirement Requirements { get; private set; }

        internal void AddNode(Node node)
        {
            Guard.NotNull(node, nameof(node));

            this.ConnectedNodes.Add(node);
        }

        internal void ConnectToPeersAsync()
        {
            if (Interlocked.CompareExchange(ref this.busyConnectingToPeers, 1, 0) == 1)
                return;

            this.asyncLoop = this.asyncLoopFactory.Run(nameof(this.ConnectToPeersAsync), token =>
            {
                ConnectToPeers();
                return Task.CompletedTask;
            },
            this.disconnect.Token,
            repeatEvery: TimeSpans.RunOnce);
        }

        private void ConnectToPeers()
        {
            while (!this.disconnect.IsCancellationRequested)
            {
                if (this.ConnectedNodes.Count >= this.MaximumNodeConnections)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    continue;
                }

                Node node = null;

                try
                {
                    var peer = FindPeerToConnectTo();
                    if (peer == null)
                        continue;

                    using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.disconnect.Token))
                    {
                        timeoutTokenSource.CancelAfter(5000);

                        this.ParentParameters.PeerAddressManager().PeerAttempted(peer.Endpoint, DateTimeOffset.Now);

                        var clonedConnectParamaters = this.currentParameters.Clone();
                        clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                        node = Node.Connect(this.network, peer, clonedConnectParamaters);
                        node.VersionHandshake(this.Requirements, timeoutTokenSource.Token);
                    }
                }
                catch (Exception exception)
                {
                    if (node != null)
                        node.DisconnectAsync("Error while connecting", exception);
                }
            }
        }

        private void Disconnect()
        {
            this.ConnectedNodes.DisconnectAll();
        }

        internal bool RemoveNode(Node node)
        {
            return this.ConnectedNodes.Remove(node);
        }

        private NetworkAddress FindPeerToConnectTo()
        {
            int groupFail = 0;

            NetworkAddress peer = null;

            while (groupFail < 50 && !this.disconnect.IsCancellationRequested)
            {
                peer = this.ParentParameters.PeerAddressManager().SelectPeerToConnectTo();
                if (peer == null)
                    break;

                if (!peer.Endpoint.Address.IsValid())
                    continue;

                var nodeExistsInGroup = this.RelatedGroups.GlobalConnectedNodes().Any(a => this.groupSelector(a).SequenceEqual(this.groupSelector(peer.Endpoint)));
                if (nodeExistsInGroup)
                {
                    groupFail++;
                    continue;
                }

                break;
            }

            return peer;
        }

        #region IDisposable Members

        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            Disconnect();
        }

        #endregion
    }
}