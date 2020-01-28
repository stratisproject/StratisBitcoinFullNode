using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.ColdStaking.Services;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Services;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class ColdStakingBroadcaster : ClientBroadcasterBase
    {
        private readonly IColdStakingService coldStakingService;
        private readonly IWalletService walletService;
        private string currentWalletName;

        public ColdStakingBroadcaster(
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime,
            IAsyncProvider asyncProvider,
            IWalletService walletService,
            IColdStakingService coldStakingService,
            EventsHub eventsHub)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.walletService = walletService;
            this.coldStakingService = coldStakingService;
        }

        protected override async Task<IEnumerable<IClientEvent>> GetMessages(CancellationToken cancellationToken)
        {
            var clientEvents = new List<ColdStakingInfoClientEvent>();

            // If no wallet name is specified iterate over all the wallets. This is to
            // ensure backward compatibility with the older Core wallet.
            if (string.IsNullOrEmpty(this.currentWalletName))
            {
                foreach (string walletName in await this.walletService.GetWalletNames(cancellationToken))
                {
                    clientEvents.Add(await this.GetColdStakingInformationAsync(walletName, cancellationToken));
                }
            }
            // Else only ask for the specified wallet.
            else
                clientEvents.Add(await this.GetColdStakingInformationAsync(this.currentWalletName, cancellationToken));

            return clientEvents;
        }

        private async Task<ColdStakingInfoClientEvent> GetColdStakingInformationAsync(string walletName, CancellationToken cancellationToken)
        {
            ColdStakingInfoClientEvent clientEvent = null;

            try
            {
                Task<GetColdStakingInfoResponse> coldStakingInfo = this.coldStakingService.GetColdStakingInfo(walletName, cancellationToken);

                await Task.WhenAll(coldStakingInfo);

                clientEvent = new ColdStakingInfoClientEvent(coldStakingInfo.Result)
                {
                    
                };
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception occurred: {0}");
            }

            return clientEvent;
        }

        protected override void OnInitialise()
        {
            base.OnInitialise();

            this.eventsHub.SubscribeToIncomingSignalRMessages(this.GetType().Name, (messageArgs) =>
            {
                if (messageArgs.Args.ContainsKey("currentWallet"))
                {
                    this.currentWalletName = messageArgs.Args["currentWallet"];
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.eventsHub.UnSubscribeToIncomingSignalRMessages(this.GetType().Name);
            }
        }
    }
}