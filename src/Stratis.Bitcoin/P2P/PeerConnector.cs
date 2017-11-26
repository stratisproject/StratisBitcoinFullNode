﻿using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Connects to peers asynchronously.</summary>
    public sealed class PeerConnector : IDisposable
    {
        /// <summary>The async loop we need to wait upon before we can shut down this connector.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The collection of peers the node is currently connected to.</summary>
        internal readonly NodesCollection ConnectedPeers;

        /// <summary>The cloned parameters used to connect to peers. </summary>
        private readonly NodeConnectionParameters currentParameters;

        /// <summary>How to calculate a group of an IP, by default using NBitcoin.IpExtensions.GetGroup.</summary>
        private readonly Func<IPEndPoint, byte[]> groupSelector;

        /// <summary>The maximum amount of peers the node can connect to (defaults to 8).</summary>
        internal int MaximumNodeConnections { get; set; }

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The network the node is running on.</summary>
        private Network network;

        internal NodeConnectionParameters ParentParameters { get; private set; }

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>What peer types (by <see cref="PeerIntroductionType"/> this connector should find and connect to.</summary>
        private readonly PeerIntroductionType peerIntroductionType;

        internal RelatedPeerConnectors RelatedPeerConnector { get; set; }

        /// <summary>Specification of requirements the <see cref="PeerConnector"/> has when connect to other peers.</summary>
        internal readonly NodeRequirement Requirements;

        /// <summary>Constructor used for unit testing.</summary>
        internal PeerConnector(
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType)
        {
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));

            this.nodeLifetime = new NodeLifetime();
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
            this.RelatedPeerConnector = new RelatedPeerConnectors();
        }

        internal PeerConnector(Network network,
            INodeLifetime nodeLifeTime,
            NodeConnectionParameters parameters,
            NodeRequirement nodeRequirements,
            Func<IPEndPoint, byte[]> groupSelector,
            IAsyncLoopFactory asyncLoopFactory,
            IPeerAddressManager peerAddressManager,
            PeerIntroductionType peerIntroductionType)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.ConnectedPeers = new NodesCollection();
            this.groupSelector = groupSelector;
            this.MaximumNodeConnections = 8;
            this.network = network;
            this.nodeLifetime = nodeLifeTime;
            this.ParentParameters = parameters;
            this.peerAddressManager = peerAddressManager;
            this.peerIntroductionType = peerIntroductionType;
            this.Requirements = nodeRequirements;

            this.currentParameters = this.ParentParameters.Clone();
            this.currentParameters.TemplateBehaviors.Add(new PeerConnectorBehaviour(this));
            this.currentParameters.ConnectCancellation = this.nodeLifetime.ApplicationStopping;
        }

        internal void AddNode(Node node)
        {
            Guard.NotNull(node, nameof(node));

            this.ConnectedPeers.Add(node);
        }

        internal void StartConnectAsync()
        {
            this.asyncLoop = this.asyncLoopFactory.Run($"{this.GetType().Name}.{nameof(this.ConnectAsync)}", async token =>
            {
                if (this.ConnectedPeers.Count < this.MaximumNodeConnections)
                    await this.ConnectAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second);
        }

        private Task ConnectAsync()
        {
            Node node = null;

            try
            {
                var peer = this.FindPeerToConnectTo();
                if (peer == null)
                    return Task.CompletedTask;

                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping))
                {
                    timeoutTokenSource.CancelAfter(5000);

                    this.peerAddressManager.PeerAttempted(peer.Endpoint, DateTimeProvider.Default.GetUtcNow());

                    var clonedConnectParamaters = this.currentParameters.Clone();
                    clonedConnectParamaters.ConnectCancellation = timeoutTokenSource.Token;

                    node = Node.Connect(this.network, peer, clonedConnectParamaters);
                    node.VersionHandshake(this.Requirements, timeoutTokenSource.Token);

                    return Task.CompletedTask;
                }
            }
            catch (Exception exception)
            {
                if (node != null)
                    node.DisconnectAsync("Error while connecting", exception);
            }

            return Task.CompletedTask;
        }

        private void Disconnect()
        {
            this.ConnectedPeers.DisconnectAll();
        }

        internal void RemoveNode(Node node)
        {
            this.ConnectedPeers.Remove(node);
        }

        internal NetworkAddress FindPeerToConnectTo()
        {
            int groupFail = 0;

            NetworkAddress peer = null;

            while (groupFail < 50 && !this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                peer = this.peerAddressManager.SelectPeerToConnectTo(this.peerIntroductionType);
                if (peer == null)
                    break;

                if (!peer.Endpoint.Address.IsValid())
                    continue;

                var nodeExistsInGroup = this.RelatedPeerConnector.GlobalConnectedNodes().Any(a => this.groupSelector(a).SequenceEqual(this.groupSelector(peer.Endpoint)));
                if (nodeExistsInGroup)
                {
                    groupFail++;
                    continue;
                }

                break;
            }

            return peer;
        }

        public void Dispose()
        {
            this.asyncLoop?.Dispose();
            this.Disconnect();
        }
    }
}
