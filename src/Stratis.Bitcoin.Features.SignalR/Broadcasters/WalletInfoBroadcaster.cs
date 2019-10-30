using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class WalletInfoBroadcaster : ClientBroadcasterBase
    {
        private readonly IWalletManager walletManager;
        private readonly IConnectionManager connectionManager;
        private readonly Network network;
        private readonly ChainIndexer chainIndexer;
        private ILogger logger;

        public WalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IConnectionManager connectionManager,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            ChainIndexer chainIndexer,
            EventsHub eventsHub)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.chainIndexer = chainIndexer;
        }

        protected override IEnumerable<IClientEvent> GetMessages()
        {
            foreach (string walletName in this.walletManager.GetWalletsNames())
            {
                WalletGeneralInfoClientEvent clientEvent = null;
                try
                {
                    Wallet.Wallet wallet = this.walletManager.GetWallet(walletName);
                    IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(walletName);
                    IList<AccountBalanceModel> accountBalanceModels = new List<AccountBalanceModel>();
                    foreach (var balance in balances)
                    {
                        HdAccount account = balance.Account;

                        var accountBalanceModel = new AccountBalanceModel
                        {
                            CoinType = (CoinType) wallet.Network.Consensus.CoinType,
                            Name = account.Name,
                            HdPath = account.HdPath,
                            AmountConfirmed = balance.AmountConfirmed,
                            AmountUnconfirmed = balance.AmountUnconfirmed,
                            SpendableAmount = balance.SpendableAmount,
                            Addresses = account.GetCombinedAddresses().Select(address =>
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
                        };

                        accountBalanceModels.Add(accountBalanceModel);
                    }

                    clientEvent = new WalletGeneralInfoClientEvent
                    {
                        WalletName = walletName,
                        Network = wallet.Network,
                        CreationTime = wallet.CreationTime,
                        LastBlockSyncedHeight = wallet.AccountsRoot.Single().LastBlockSyncedHeight,
                        ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                        ChainTip = this.chainIndexer.Tip.Height,
                        IsChainSynced = this.chainIndexer.IsDownloaded(),
                        IsDecrypted = true,
                        AccountsBalances = accountBalanceModels
                    };

                    // Get the wallet's file path.
                    (string folder, IEnumerable<string> fileNameCollection) = this.walletManager.GetWalletsFiles();
                    string searchFile =
                        Path.ChangeExtension(walletName, this.walletManager.GetWalletFileExtension());
                    string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                    if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(fileName))
                    {
                        clientEvent.WalletFilePath = Path.Combine(folder, fileName);
                    }
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Exception occurred: {0}");
                }

                if (null != clientEvent)
                {
                    yield return clientEvent;
                }
            }
        }
    }
}