using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    class PartialTransactionsBehavior : NetworkPeerBehavior
    {
        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly ICounterChainSessionManager counterChainSessionManager;

        private readonly Network network;

        private readonly FederationGatewaySettings federationGatewaySettings;

        private readonly IPAddressComparer ipAddressComparer;

        public PartialTransactionsBehavior(
            ILoggerFactory loggerFactory,
            ICrossChainTransactionMonitor crossChainTransactionMonitor,
            IFederationWalletManager federationWalletManager,
            ICounterChainSessionManager counterChainSessionManager,
            Network network,
            FederationGatewaySettings federationGatewaySettings)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(crossChainTransactionMonitor, nameof(crossChainTransactionMonitor));
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(counterChainSessionManager, nameof(counterChainSessionManager));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.federationWalletManager = federationWalletManager;
            this.counterChainSessionManager = counterChainSessionManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.ipAddressComparer = new IPAddressComparer();
        }

        public override object Clone()
        {
            return new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.federationWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings);
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            if (federationGatewaySettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, AttachedPeer.PeerEndPoint.Address)))
            {
                this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
            }

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            if (federationGatewaySettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, AttachedPeer.PeerEndPoint.Address)))
            {
                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            }

            this.logger.LogTrace("(-)");
        }

        async Task Broadcast(RequestPartialTransactionPayload payload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(payload.BossCard), payload.BossCard, nameof(payload.Command), payload.Command, nameof(payload.BlockHeight), payload.BlockHeight);

            if (federationGatewaySettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, AttachedPeer.PeerEndPoint.Address)))
            {
                await this.AttachedPeer.SendMessageAsync(payload);
            }

            this.logger.LogTrace("(-)");
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is RequestPartialTransactionPayload payload)
            {
                this.logger.LogInformation("RequestPartialTransactionPayload received.");
                this.logger.LogInformation("OnMessageReceivedAsync: {0}", this.network.ToChain());
                this.logger.LogInformation("RequestPartialTransactionPayload: BossCard            - {0}.", payload.BossCard);
                this.logger.LogInformation("RequestPartialTransactionPayload: BlockHeight         - {0}.", payload.BlockHeight);
                this.logger.LogInformation("RequestPartialTransactionPayload: PartialTransaction  - {0}.", payload.PartialTransaction);
                this.logger.LogInformation("RequestPartialTransactionPayload: TemplateTransaction - {0}.", payload.TemplateTransaction);

                if (payload.BossCard == uint256.Zero)
                {
                    //get the template from the payload
                    this.logger.LogInformation("OnMessageReceivedAsync: Payload has no bossCard -> signing partial.");

                    var template = payload.TemplateTransaction;

                    var partialTransactionSession = this.counterChainSessionManager.VerifySession(payload.BlockHeight, template);
                    if (partialTransactionSession == null) return;
                    this.counterChainSessionManager.MarkSessionAsSigned(partialTransactionSession);

                    var wallet = this.federationWalletManager.GetWallet();

                    var signedTransaction = wallet.SignPartialTransaction(template, this.federationWalletManager.Secret.WalletPassword);
                    payload.AddPartial(signedTransaction, BossTable.MakeBossTableEntry(payload.BlockHeight, this.federationGatewaySettings.PublicKey));

                    this.logger.LogInformation("OnMessageReceivedAsync: PartialTransaction signed.");
                    this.logger.LogInformation("RequestPartialTransactionPayload: BossCard            - {0}.", payload.BossCard);
                    this.logger.LogInformation("RequestPartialTransactionPayload: PartialTransaction  - {0}.", payload.PartialTransaction);
                    this.logger.LogInformation("Broadcasting Payload....");

                    await this.Broadcast(payload);

                    this.logger.LogInformation("Broadcasted.");
                }
                else
                {
                    //we got a partial back
                    this.logger.LogInformation("RequestPartialTransactionPayload: PartialTransaction received.");
                    this.counterChainSessionManager.ReceivePartial(payload.BlockHeight, payload.PartialTransaction, payload.BossCard);
                }
            }

            this.logger.LogTrace("(-)");
        }
    }
}
