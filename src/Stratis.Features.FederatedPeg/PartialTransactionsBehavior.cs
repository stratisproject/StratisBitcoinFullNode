using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.InputConsolidation;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Payloads;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg
{
    public class PartialTransactionsBehavior : NetworkPeerBehavior
    {
        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly Network network;

        private readonly IFederatedPegSettings federatedPegSettings;

        private readonly ICrossChainTransferStore crossChainTransferStore;

        private readonly IInputConsolidator inputConsolidator;

        public PartialTransactionsBehavior(
            ILoggerFactory loggerFactory,
            IFederationWalletManager federationWalletManager,
            Network network,
            IFederatedPegSettings federatedPegSettings,
            ICrossChainTransferStore crossChainTransferStore,
            IInputConsolidator inputConsolidator)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(federatedPegSettings, nameof(federatedPegSettings));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.federationWalletManager = federationWalletManager;
            this.network = network;
            this.federatedPegSettings = federatedPegSettings;
            this.crossChainTransferStore = crossChainTransferStore;
            this.inputConsolidator = inputConsolidator;
        }

        [NoTrace]
        public override object Clone()
        {
            return new PartialTransactionsBehavior(this.loggerFactory, this.federationWalletManager, this.network,
                this.federatedPegSettings, this.crossChainTransferStore, this.inputConsolidator);
        }

        protected override void AttachCore()
        {
            if (this.federatedPegSettings.FederationNodeIpAddresses.Contains(this.AttachedPeer.PeerEndPoint.Address))
                this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        protected override void DetachCore()
        {
            if (this.federatedPegSettings.FederationNodeIpAddresses.Contains(this.AttachedPeer.PeerEndPoint.Address))
                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <summary>
        /// Broadcast the partial transaction request to federation members.
        /// </summary>
        /// <param name="payload">The payload to broadcast.</param>
        private async Task BroadcastAsync(RequestPartialTransactionPayload payload)
        {
            if (this.AttachedPeer.IsConnected && this.federatedPegSettings.FederationNodeIpAddresses.Contains(this.AttachedPeer.PeerEndPoint.Address))
                await this.AttachedPeer.SendMessageAsync(payload).ConfigureAwait(false);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is RequestPartialTransactionPayload payload))
                return;

            // Is a consolidation request.
            if (payload.DepositId == RequestPartialTransactionPayload.ConsolidationDepositId)
            {
                this.logger.LogDebug("Received request to sign consolidation transaction.");
                await this.HandleConsolidationTransactionRequest(peer, payload);
                return;
            }

            ICrossChainTransfer[] transfer = await this.crossChainTransferStore.GetAsync(new[] { payload.DepositId });

            // This could be null if the store was unable to sync with the federation 
            // wallet manager. It is possible that the federation wallet's tip is not 
            // on chain and as such the store was not able to sync.
            if (transfer == null)
            {
                this.logger.LogDebug("{0}: Unable to retrieve transfers for deposit {1} at this time, the store is not synced.", nameof(this.OnMessageReceivedAsync), payload.DepositId);
                return;
            }

            if (transfer[0] == null)
            {
                this.logger.LogDebug("{0}: Deposit {1} does not exist.", nameof(this.OnMessageReceivedAsync), payload.DepositId);
                return;
            }

            if (transfer[0].Status != CrossChainTransferStatus.Partial)
            {
                this.logger.LogDebug("{0}: Deposit {1} is {2}.", nameof(this.OnMessageReceivedAsync), payload.DepositId, transfer[0].Status);
                return;
            }

            if (transfer[0].PartialTransaction == null)
            {
                this.logger.LogDebug("{0}: Deposit {1}, PartialTransaction not found.", nameof(this.OnMessageReceivedAsync), payload.DepositId);
                return;
            }

            uint256 oldHash = transfer[0].PartialTransaction.GetHash();

            Transaction signedTransaction = await this.crossChainTransferStore.MergeTransactionSignaturesAsync(payload.DepositId, new[] { payload.PartialTransaction }).ConfigureAwait(false);

            if (signedTransaction == null)
            {
                this.logger.LogDebug("{0}: Deposit {1}, signedTransaction not found.", nameof(this.OnMessageReceivedAsync), payload.DepositId);
                return;
            }

            if (oldHash != signedTransaction.GetHash())
            {
                this.logger.LogDebug("Signed transaction (deposit={0}) to produce {1} from {2}.", payload.DepositId, signedTransaction.GetHash(), oldHash);

                // Respond back to the peer that requested a signature.
                await this.BroadcastAsync(payload.AddPartial(signedTransaction));
            }
        }

        private async Task HandleConsolidationTransactionRequest(INetworkPeer peer, RequestPartialTransactionPayload payload)
        {
            ConsolidationSignatureResult result = this.inputConsolidator.CombineSignatures(payload.PartialTransaction);

            if (result.Signed)
            {
                this.logger.LogDebug("Signed consolidating transaction to produce {0} from {1}", result.TransactionResult.GetHash(), payload.PartialTransaction.GetHash());
                await this.BroadcastAsync(payload.AddPartial(result.TransactionResult));
            }
        }
    }
}
