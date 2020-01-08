using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Features.Wallet.Models;
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

            // If no wallet name is specified iterate over all the wallets. This is to
            // ensure backward compatibility with the older Core wallet.
            if (string.IsNullOrEmpty(this.currentWalletName))
            {
                foreach (string walletName in await this.walletService.GetWalletNames(cancellationToken))
                {
                    clientEvents.Add(await this.GetWalletInformationAsync(walletName, cancellationToken));
                }
            }
            // Else only ask for the specified wallet.
            else
                clientEvents.Add(await this.GetWalletInformationAsync(this.currentWalletName, cancellationToken));

            return clientEvents;
        }

        private async Task<WalletGeneralInfoClientEvent> GetWalletInformationAsync(string walletName, CancellationToken cancellationToken)
        {
            WalletGeneralInfoClientEvent clientEvent = null;

            try
            {
                Task<WalletGeneralInfoModel> generalInfo = this.walletService.GetWalletGeneralInfo(walletName, cancellationToken);
                Task<WalletBalanceModel> balances = this.walletService.GetBalance(walletName, null, this.includeAddressBalances, cancellationToken);

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