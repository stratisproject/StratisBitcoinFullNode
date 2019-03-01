using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.NetworkHelpers;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg
{
    public class PartialTransactionsBehavior : NetworkPeerBehavior
    {
        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly Network network;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IPAddressComparer ipAddressComparer;

        private readonly ICrossChainTransferStore crossChainTransferStore;

        public PartialTransactionsBehavior(
            ILoggerFactory loggerFactory,
            IFederationWalletManager federationWalletManager,
            Network network,
            IFederationGatewaySettings federationGatewaySettings,
            ICrossChainTransferStore crossChainTransferStore)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.federationWalletManager = federationWalletManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.crossChainTransferStore = crossChainTransferStore;
            this.ipAddressComparer = new IPAddressComparer();
        }

        public override object Clone()
        {
            return new PartialTransactionsBehavior(this.loggerFactory, this.federationWalletManager, this.network,
                this.federationGatewaySettings, this.crossChainTransferStore);
        }

        protected override void AttachCore()
        {
            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
                this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        protected override void DetachCore()
        {
            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        /// <summary>
        /// Broadcast the partial transaction request to federation members.
        /// </summary>
        /// <param name="payload">The payload to broadcast.</param>
        private async Task BroadcastAsync(RequestPartialTransactionPayload payload)
        {
            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
                await this.AttachedPeer.SendMessageAsync(payload).ConfigureAwait(false);
        }

        private Transaction GetTemplateTransaction(Transaction partialTransaction)
        {
            Transaction templateTransaction = this.network.CreateTransaction(partialTransaction.ToBytes(this.network.Consensus.ConsensusFactory));

            foreach (TxIn input in templateTransaction.Inputs)
                input.ScriptSig = new Script();

            return templateTransaction;
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            var payload = message.Message.Payload as RequestPartialTransactionPayload;

            if (payload == null)
                return;

            // Get the template from the payload.
            Transaction template = this.GetTemplateTransaction(payload.PartialTransaction);

            ICrossChainTransfer[] transfer = await this.crossChainTransferStore.GetAsync(new[] { payload.DepositId });

            if (transfer[0] == null)
            {
                this.logger.LogTrace("OnMessageReceivedAsync: Transaction {0} does not exist.", template);
                return;
            }

            if (transfer[0].Status != CrossChainTransferStatus.Partial)
            {
                this.logger.LogTrace("OnMessageReceivedAsync: Transaction {0} is {1}.", template, transfer[0].Status);
                return;
            }

            uint256 oldHash = transfer[0].PartialTransaction.GetHash();

            Transaction signedTransaction = await this.crossChainTransferStore.MergeTransactionSignaturesAsync(payload.DepositId, new[] { payload.PartialTransaction }).ConfigureAwait(false);

            if (oldHash != signedTransaction.GetHash())
            {
                this.logger.LogInformation("Signed transaction (deposit={1}) to produce {2} from {3}.", payload.DepositId, oldHash, signedTransaction.GetHash());

                // Respond back to the peer that requested a signature.
                await this.BroadcastAsync(payload.AddPartial(signedTransaction));
            }
        }
    }
}
