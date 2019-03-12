using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.NetworkHelpers;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Requests partial transactions from the peers and calls <see cref="ICrossChainTransferStore.MergeTransactionSignaturesAsync".
    /// </summary>
    public interface IPartialTransactionRequester
    {
        /// <summary>
        /// Broadcast the partial transaction request to federation members.
        /// </summary>
        /// <param name="payload">The payload to broadcast.</param>
        Task BroadcastAsync(RequestPartialTransactionPayload payload);

        /// <summary>
        /// Starts the broadcasting of partial transaction requests.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the broadcasting of partial transaction requests.
        /// </summary>
        void Stop();
    }

    /// <inheritdoc />
    public class PartialTransactionRequester : IPartialTransactionRequester
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly INodeLifetime nodeLifetime;
        private readonly IConnectionManager connectionManager;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        private IAsyncLoop asyncLoop;

        public PartialTransactionRequester(
            ILoggerFactory loggerFactory,
            ICrossChainTransferStore crossChainTransferStore,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IConnectionManager connectionManager,
            IFederationGatewaySettings federationGatewaySettings)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.crossChainTransferStore = crossChainTransferStore;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        /// <inheritdoc />
        public async Task BroadcastAsync(RequestPartialTransactionPayload payload)
        {
            List<INetworkPeer> peers = this.connectionManager.ConnectedPeers.ToList();

            var ipAddressComparer = new IPAddressComparer();

            foreach (INetworkPeer peer in peers)
            {
                // Broadcast to peers.
                if (!peer.IsConnected)
                    continue;

                if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, peer.PeerEndPoint.Address)))
                {
                    try
                    {
                        await peer.SendMessageAsync(payload).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(PartialTransactionRequester), token =>
            {
                // Broadcast the partial transaction with the earliest inputs.
                KeyValuePair<uint256, Transaction> kv = this.crossChainTransferStore.GetTransactionsByStatusAsync(
                    CrossChainTransferStatus.Partial, true).GetAwaiter().GetResult().FirstOrDefault();

                if (kv.Key != null)
                {
                    this.BroadcastAsync(new RequestPartialTransactionPayload(kv.Key).AddPartial(kv.Value)).GetAwaiter().GetResult();
                    this.logger.LogInformation("Partial template requested");
                }

                this.logger.LogTrace("(-)[PARTIAL_TEMPLATES_JOB]");
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpans.TenSeconds);
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.asyncLoop != null)
            {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}
