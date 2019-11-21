using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Features.Wallet.Services;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class WalletInfoBroadcaster : ClientBroadcasterBase
    {
        private readonly IWalletService walletService;
        private readonly bool includeAddressBalances;
        private string currentWalletName;
        private string currentAddress;
        private string currentAccount;

        public WalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletService walletService,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            EventsHub eventsHub, bool includeAddressBalances = false)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.walletService = walletService;
            this.includeAddressBalances = includeAddressBalances;
        }

        protected override async Task<IEnumerable<IClientEvent>> GetMessages(CancellationToken cancellationToken)
        {
            var clientEvents = new List<WalletGeneralInfoClientEvent>();

            foreach (string walletName in await this.walletService.GetWalletNames(cancellationToken))
            {
                if (null != this.currentWalletName && walletName != this.currentWalletName)
                {
                    continue;
                }
                
                WalletGeneralInfoClientEvent clientEvent = null;
                
                try
                {
                    var generalInfo = this.walletService.GetWalletGeneralInfo(walletName, cancellationToken);
                    var balances = this.walletService.GetBalance(walletName, null,
                        this.includeAddressBalances, cancellationToken);

                    await Task.WhenAll(generalInfo, balances);

                    clientEvent = new WalletGeneralInfoClientEvent(generalInfo.Result)
                    {
                        AccountsBalances = balances.Result.AccountsBalances
                    };
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Exception occurred: {0}");
                }

                clientEvents.Add(clientEvent);
            }

            return clientEvents;
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
                
                if (messageArgs.Args.ContainsKey("currentAccount"))
                {
                    this.currentAccount = messageArgs.Args["currentAccount"];
                }
                
                if (messageArgs.Args.ContainsKey("currentAddress"))
                {
                    this.currentAddress = messageArgs.Args["currentAddress"];
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