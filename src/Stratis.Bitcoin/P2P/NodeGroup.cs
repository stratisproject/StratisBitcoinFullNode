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
        static Random _Rand = new Random();
        static Func<IPEndPoint, byte[]> _GroupByRandom;
        public static Func<IPEndPoint, byte[]> ByRandom
        {
            get
            {
                return _GroupByRandom = _GroupByRandom ?? new Func<IPEndPoint, byte[]>((ip) =>
                {

                    var group = new byte[20];
                    _Rand.NextBytes(group);
                    return group;
                });
            }
        }

        static Func<IPEndPoint, byte[]> _GroupByIp;
        public static Func<IPEndPoint, byte[]> ByIp
        {
            get
            {
                return _GroupByIp = _GroupByIp ?? new Func<IPEndPoint, byte[]>((ip) =>
                {
                    return ip.Address.GetAddressBytes();
                });
            }
        }

        static Func<IPEndPoint, byte[]> _GroupByEndpoint;
        public static Func<IPEndPoint, byte[]> ByEndpoint
        {
            get
            {
                return _GroupByEndpoint = _GroupByEndpoint ?? new Func<IPEndPoint, byte[]>((endpoint) =>
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

        static Func<IPEndPoint, byte[]> _GroupByNetwork;
        public static Func<IPEndPoint, byte[]> ByNetwork
        {
            get
            {
                return _GroupByNetwork = _GroupByNetwork ?? new Func<IPEndPoint, byte[]>((ip) =>
                {
                    return IpExtensions.GetGroup(ip.Address);
                });
            }
        }
    }

    public sealed class RelatedNodeGroups : Dictionary<string, NodeGroup>
    {
        public void Register(string name, NodeGroup nodeGroup)
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

    public sealed class NodeGroup : IDisposable
    {
        internal NodeGroup(Network network, INodeLifetime nodeLifeTime, NodeConnectionParameters parameters, NodeRequirement nodeRequirements, Func<IPEndPoint, byte[]> groupSelector, IAsyncLoopFactory asyncLoopFactory)
        {
            this.disconnect = CancellationTokenSource.CreateLinkedTokenSource(nodeLifeTime.ApplicationStopping);
            this.MaximumNodeConnections = 8;

            this.asyncLoopFactory = asyncLoopFactory;
            this.connectedNodes = new NodesCollection();
            this.groupSelector = groupSelector;
            this.network = network;
            this.parameters = parameters;
            this.requirements = nodeRequirements;

            CloneParameters();
        }

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary> True if cancellation has not been requested and the maximum number of connected nodes has not been reached.</summary>
        private bool CanConnect
        {
            get { return this.disconnect.IsCancellationRequested == false && this.connectedNodes.Count < this.MaximumNodeConnections; }
        }

        private NodesCollection connectedNodes;
        internal NodesCollection ConnectedNodes
        {
            get { return this.connectedNodes; }
        }

        private CancellationTokenSource disconnect;

        /// <summary> How to calculate a group of an IP, by default using NBitcoin.IpExtensions.GetGroup.</summary>
        private Func<IPEndPoint, byte[]> groupSelector;
        private TraceCorrelation trace = new TraceCorrelation(NodeServerTrace.Trace, "Group connection");

        private NodeConnectionParameters parameters;
        internal NodeConnectionParameters Parameters
        {
            get { return this.parameters; }
        }

        private NodeConnectionParameters connectParameters;

        internal int MaximumNodeConnections { get; set; }
        private Network network;

        internal RelatedNodeGroups RelatedGroups { get; set; }

        private NodeRequirement requirements;
        internal NodeRequirement Requirements
        {
            get { return this.requirements; }
        }

        internal void ConnectToPeersAsync()
        {
            if (this.asyncLoop != null)
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
            while (this.CanConnect)
            {
                Node node = null;

                try
                {
                    var peer = SelectPeer();
                    if (peer == null)
                        continue;

                    ConnectPeer(peer, out node);
                }
                catch (OperationCanceledException cancelled)
                {
                    if (this.disconnect.Token.IsCancellationRequested)
                        break;

                    if (node != null)
                        node.DisconnectAsync("Handshake timeout", cancelled);
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
            this.connectedNodes.DisconnectAll();
        }

        internal void Handshaked(Node node)
        {
            Guard.NotNull(node, nameof(node));

            this.connectedNodes.Add(node);
        }

        internal bool RemoveNode(Node node)
        {
            return this.connectedNodes.Remove(node);
        }

        private NetworkAddress SelectPeer()
        {
            int groupFail = 0;

            NetworkAddress peer = null;

            while (true)
            {
                if (groupFail > 50)
                {
                    this.connectParameters.ConnectCancellation.WaitHandle.WaitOne((int)TimeSpan.FromSeconds(60).TotalMilliseconds);
                    break;
                }

                peer = this.parameters.PeerAddressManager().SelectPeerToConnectTo();
                if (peer == null)
                {
                    this.connectParameters.ConnectCancellation.WaitHandle.WaitOne(1000);
                    break;
                }

                if (peer.Endpoint.Address.IsValid() == false)
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

        private void ConnectPeer(NetworkAddress peer, out Node node)
        {
            using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.disconnect.Token))
            {
                timeoutTokenSource.CancelAfter(5000);

                this.Parameters.PeerAddressManager().PeerAttempted(peer.Endpoint, DateTimeOffset.Now);

                var clonedConnectParamaters = this.connectParameters.Clone();
                clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                node = Node.Connect(this.network, peer, clonedConnectParamaters);
                node.VersionHandshake(this.requirements, timeoutTokenSource.Token);
            }
        }

        private void CloneParameters()
        {
            this.connectParameters = this.parameters.Clone();
            this.connectParameters.TemplateBehaviors.Add(new NodeGroupBehavior(this));
            this.connectParameters.ConnectCancellation = this.disconnect.Token;
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