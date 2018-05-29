using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using GpCoinType = Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType;

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

        private IGeneralPurposeWalletManager generalPurposeWalletManager;

        private ICounterChainSessionManager counterChainSessionManager;

        private Network network;

        private FederationGatewaySettings federationGatewaySettings;

        public PartialTransactionsBehavior(ILoggerFactory loggerFactory, ICrossChainTransactionMonitor crossChainTransactionMonitor,
            IGeneralPurposeWalletManager generalPurposeWalletManager, ICounterChainSessionManager counterChainSessionManager, Network network, FederationGatewaySettings federationGatewaySettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.generalPurposeWalletManager = generalPurposeWalletManager;
            this.counterChainSessionManager = counterChainSessionManager;
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        public override object Clone()
        {
            return new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.generalPurposeWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings);
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
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        async Task Broadcast(RequestPartialTransactionPayload payload)
        {
            await this.AttachedPeer.SendMessageAsync(payload);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (message.Message.Payload is RequestPartialTransactionPayload)
            {
                this.logger.LogInformation("()");
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload received.");
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: {this.network.ToChain()}");
            
                var payload = message.Message.Payload as RequestPartialTransactionPayload;

                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: SessionId           - {payload.SessionId}.");
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: TemplateTransaction - {payload.TemplateTransaction}.");

                if (payload.BossCard == uint256.Zero)
                {
                    //get the template from the payload
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: Payload has no bossCard -> signing partial.");

                    var template = payload.TemplateTransaction;

                    var partialTransactionSession = this.counterChainSessionManager.VerifySession(payload.SessionId, template);
                    if (partialTransactionSession == null) return;
                    this.counterChainSessionManager.MarkSessionAsSigned(partialTransactionSession);

                    var wallet = this.generalPurposeWalletManager.GetWallet(this.federationGatewaySettings.MultiSigWalletName);
                    var account = wallet.GetAccountsByCoinType((GpCoinType) this.network.Consensus.CoinType).First();
                    var signedTransaction = account.SignPartialTransaction(template);
                    payload.AddPartial(signedTransaction, BossTable.MakeBossTableEntry(payload.SessionId, this.federationGatewaySettings.PublicKey));

                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} OnMessageReceivedAsync: PartialTransaction signed.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Broadcasting Payload....");
                    await this.Broadcast(payload);
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} Broadcasted.");
                }
                else
                {
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: SessionId           - {payload.SessionId}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: BossCard            - {payload.BossCard}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: PartialTransaction  - {payload.PartialTransaction}.");
                    this.logger.LogInformation($"{this.federationGatewaySettings.MemberName} RequestPartialTransactionPayload: TemplateTransaction - {payload.TemplateTransaction}.");

                    //we got a partial back
                    this.counterChainSessionManager.ReceivePartial(payload.SessionId, payload.PartialTransaction, payload.BossCard);
                }

                this.logger.LogInformation("(-)");
            }
        }
    }
}
