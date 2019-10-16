using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class WalletInfoBroadcaster : ClientBroadcasterBase
    {
        private readonly IWalletManager walletManager;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly ChainIndexer chainIndexer;
        private readonly bool includeAddressBalances;

        public WalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IConsensusManager consensusManager,
            IConnectionManager connectionManager,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            ChainIndexer chainIndexer,
            EventsHub eventsHub, bool includeAddressBalances = false)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.consensusManager = consensusManager;
            this.chainIndexer = chainIndexer;
            this.includeAddressBalances = includeAddressBalances;
        }

        protected override async Task<IEnumerable<IClientEvent>> GetMessages()
        {
            var events = new List<WalletGeneralInfoClientEvent>();
            foreach (string walletName in this.walletManager.GetWalletsNames())
            {
                WalletGeneralInfoClientEvent clientEvent = null;
                try
                {
                    clientEvent = await Task.Run(() =>
                    {
                        Wallet.Wallet wallet = this.walletManager.GetWallet(walletName);
                        IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(walletName);
                        IList<AccountBalanceModel> accountBalanceModels = new List<AccountBalanceModel>();
                        foreach (var balance in balances)
                        {
                            HdAccount account = wallet.GetAccount(balance.Account.Name);

                            var accountBalanceModel = new AccountBalanceModel
                            {
                                CoinType = (CoinType) wallet.Network.Consensus.CoinType,
                                Name = account.Name,
                                HdPath = account.HdPath,
                                AmountConfirmed = balance.AmountConfirmed,
                                AmountUnconfirmed = balance.AmountUnconfirmed,
                                SpendableAmount = balance.SpendableAmount,
                                Addresses = this.includeAddressBalances
                                    ? account.GetCombinedAddresses().Select(address =>
                                    {
                                        (Money confirmedAmount, Money unConfirmedAmount) = address.GetBalances();
                                        return new AddressModel
                                        {
                                            Address = address.Address,
                                            IsUsed = address.Transactions.Any(),
                                            IsChange = address.IsChangeAddress(),
                                            AmountConfirmed = confirmedAmount,
                                            AmountUnconfirmed = unConfirmedAmount
                                        };
                                    })
                                    : null
                            };

                            accountBalanceModels.Add(accountBalanceModel);
                        }

                        return new WalletGeneralInfoClientEvent
                        {
                            WalletName = walletName,
                            Network = wallet.Network,
                            CreationTime = wallet.CreationTime,
                            LastBlockSyncedHeight = wallet.AccountsRoot.Single().LastBlockSyncedHeight,
                            ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                            ChainTip = this.consensusManager.HeaderTip,
                            IsChainSynced = this.chainIndexer.IsDownloaded(),
                            IsDecrypted = true,
                            AccountsBalances = accountBalanceModels
                        };
                    });
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Exception occurred: {0}");
                }

                if (null != clientEvent)
                {
                    events.Add(clientEvent);
                }
            }
            return events;
        }
    }
}