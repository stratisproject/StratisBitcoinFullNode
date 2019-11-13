using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    using System.Threading;
    using System.Threading.Tasks;
    using Wallet.Services;

    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class WalletInfoBroadcaster : ClientBroadcasterBase
    {
        private readonly IWalletService walletService;
        private readonly IWalletManager walletManager;
        private readonly bool includeAddressBalances;

        public WalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletService walletService,
            IWalletManager walletManager,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            EventsHub eventsHub, bool includeAddressBalances = false)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.walletService = walletService;
            this.walletManager = walletManager;
            this.includeAddressBalances = includeAddressBalances;
        }

        protected override async Task<IEnumerable<IClientEvent>> GetMessages(CancellationToken cancellationToken)
        {
            var clientEvents = new List<WalletGeneralInfoClientEvent>();
            foreach (string walletName in this.walletManager.GetWalletsNames())
            {
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
    }
}