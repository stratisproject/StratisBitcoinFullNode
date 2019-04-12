using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.NetworkHelpers;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg.TargetChain {
    /// <summary>
    /// Requests partial transactions from the peers and calls <see cref="ICrossChainTransferStore.MergeTransactionSignaturesAsync".
    /// </summary>
    public interface IPartialTransactionRequester {
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
    public class PartialTransactionRequester : IPartialTransactionRequester {
        /// <summary>
        /// How often to trigger the query for and broadcasting of partial transactions.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.TenSeconds;

        private readonly ILogger logger;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly IConnectionManager connectionManager;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;

        private IAsyncLoop asyncLoop;

        public PartialTransactionRequester(
            ILoggerFactory loggerFactory,
            ICrossChainTransferStore crossChainTransferStore,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IConnectionManager connectionManager,
            IFederationGatewaySettings federationGatewaySettings,
            IInitialBlockDownloadState ibdState,
            IFederationWalletManager federationWalletManager) {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.crossChainTransferStore = crossChainTransferStore;
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.ibdState = ibdState;
            this.federationWalletManager = federationWalletManager;
        }

        /// <inheritdoc />
        public async Task BroadcastAsync(RequestPartialTransactionPayload payload) {
            List<INetworkPeer> peers = this.connectionManager.ConnectedPeers.ToList();

            var ipAddressComparer = new IPAddressComparer();

            foreach (INetworkPeer peer in peers) {
                // Broadcast to peers.
                if (!peer.IsConnected)
                    continue;

                if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, peer.PeerEndPoint.Address))) {
                    try {
                        await peer.SendMessageAsync(payload).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) {
                    }
                }
            }
        }

        public async Task BroadcastPartialTransactionsAsync() {
            if (this.ibdState.IsInitialBlockDownload() || !this.federationWalletManager.IsFederationWalletActive()) {
                this.logger.LogTrace("Federation wallet isn't active or in IBD. Not attempting to request transaction signatures.");
                return;
            }

            // Broadcast the partial transaction with the earliest inputs.
            KeyValuePair<uint256, Transaction> kv = (await this.crossChainTransferStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.Partial, true))
                .FirstOrDefault();

            if (kv.Key != null) {
                await this.BroadcastAsync(new RequestPartialTransactionPayload(kv.Key).AddPartial(kv.Value));
                this.logger.LogInformation("Partial template requested");
            }
        }

        /// <inheritdoc />
        public void Start() {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(PartialTransactionRequester), token => {
                this.BroadcastPartialTransactionsAsync().GetAwaiter().GetResult();
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            TimeBetweenQueries);
        }

        /// <inheritdoc />
        public void Stop() {
            if (this.asyncLoop != null) {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}
