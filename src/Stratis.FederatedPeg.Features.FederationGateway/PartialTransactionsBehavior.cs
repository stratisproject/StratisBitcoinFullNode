using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    class PartialTransactionsBehavior : NetworkPeerBehavior
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
            this.logger.LogTrace("()");

            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
            {
                this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
            }

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
            {
                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Broadcast the partial transaction request to federation members.
        /// </summary>
        /// <param name="payload">The payload to broadcast.</param>
        async Task BroadcastAsync(RequestPartialTransactionPayload payload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(payload.Command), payload.Command, nameof(payload.DepositId), payload.DepositId);

            if (this.federationGatewaySettings.FederationNodeIpEndPoints.Any(e => this.ipAddressComparer.Equals(e.Address, this.AttachedPeer.PeerEndPoint.Address)))
            {
                await this.AttachedPeer.SendMessageAsync(payload).ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        private Transaction GetTemplateTransaction(Transaction partialTransaction)
        {
            Transaction templateTransaction = this.network.CreateTransaction(partialTransaction.ToBytes(this.network.Consensus.ConsensusFactory));

            foreach (TxIn input in templateTransaction.Inputs)
            {
                input.ScriptSig = new Script();
            }

            return templateTransaction;
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            var payload = message.Message.Payload as RequestPartialTransactionPayload;
            if (payload == null) return;

            // Get the template from the payload.
            Transaction template = this.GetTemplateTransaction(payload.PartialTransaction);

            this.logger.LogInformation("RequestPartialTransactionPayload received.");
            this.logger.LogInformation("OnMessageReceivedAsync: {0}", this.network.ToChain());
            this.logger.LogInformation("RequestPartialTransactionPayload: DepositID           - {0}.", payload.DepositId);
            this.logger.LogInformation("RequestPartialTransactionPayload: PartialTransaction  - {0}.", payload.PartialTransaction);
            this.logger.LogInformation("RequestPartialTransactionPayload: TemplateTransaction - {0}.", template);

            uint256 oldHash = payload.PartialTransaction.GetHash();

            payload.AddPartial(await this.crossChainTransferStore.MergeTransactionSignaturesAsync(payload.DepositId, new[] { payload.PartialTransaction }).ConfigureAwait(false));

            if (payload.PartialTransaction.GetHash() == oldHash)
            {
                this.logger.LogInformation("OnMessageReceivedAsync: PartialTransaction already signed.");
            }
            else
            {
                this.logger.LogInformation("OnMessageReceivedAsync: PartialTransaction signed.");
                this.logger.LogInformation("RequestPartialTransactionPayload: PartialTransaction  - {0}.", payload.PartialTransaction);
                this.logger.LogInformation("Broadcasting Payload....");

                await BroadcastAsync(payload);

                this.logger.LogInformation("Broadcasted.");
            }
        }
    }
}
