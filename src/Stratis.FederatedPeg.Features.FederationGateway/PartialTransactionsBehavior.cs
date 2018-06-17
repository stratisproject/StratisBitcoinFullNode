using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    class PartialTransactionsBehavior : NetworkPeerBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private IFederationWalletManager federationWalletManager;

        private ICounterChainSessionManager counterChainSessionManager;

        private Network network;

        private FederationGatewaySettings federationGatewaySettings;

        public PartialTransactionsBehavior(ILoggerFactory loggerFactory, ICrossChainTransactionMonitor crossChainTransactionMonitor,
            IFederationWalletManager federationWalletManager, ICounterChainSessionManager counterChainSessionManager, Network network, FederationGatewaySettings federationGatewaySettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.federationWalletManager = federationWalletManager;
            this.counterChainSessionManager = counterChainSessionManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        public override object Clone()
        {
            return new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.federationWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings);
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            var peer = this.AttachedPeer;
            if (peer.State == NetworkPeerState.Connected)
                this.logger.LogDebug("PartialTransactionsBehavior Attached.");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }

        async Task Broadcast(RequestPartialTransactionPayload payload)
        {
            await this.AttachedPeer.SendMessageAsync(payload);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is RequestPartialTransactionPayload payload)
            {
                this.logger.LogTrace("()");
                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload received.");
                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: {this.network.ToChain()}");

                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: SessionId           - {payload.SessionId}.");
                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                this.logger.LogInformation(
                    $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: TemplateTransaction - {payload.TemplateTransaction}.");

                if (payload.BossCard == uint256.Zero)
                {
                    //get the template from the payload
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: Payload has no bossCard -> signing partial.");

                    var template = payload.TemplateTransaction;

                    var partialTransactionSession =
                        this.counterChainSessionManager.VerifySession(payload.SessionId, template);
                    if (partialTransactionSession == null) return;
                    this.counterChainSessionManager.MarkSessionAsSigned(partialTransactionSession);

                    var wallet = this.federationWalletManager.GetWallet();

                    // TODO: The wallet password is hardcoded here
                    var signedTransaction = wallet.SignPartialTransaction(template, "password");
                    payload.AddPartial(signedTransaction,
                        BossTable.MakeBossTableEntry(payload.SessionId, this.federationGatewaySettings.PublicKey));

                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: PartialTransaction signed.");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Broadcasting Payload....");
                    await this.Broadcast(payload);
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Broadcasted.");
                }
                else
                {
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: SessionId           - {payload.SessionId}.");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: TemplateTransaction - {payload.TemplateTransaction}.");

                    //we got a partial back
                    this.counterChainSessionManager.ReceivePartial(payload.SessionId, payload.PartialTransaction,
                        payload.BossCard);
                }
            }

            this.logger.LogTrace("(-)");
        }
    }
}
