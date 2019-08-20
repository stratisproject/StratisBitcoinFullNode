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
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    public class PeerStatisticsClientBroadcaster : ClientBroadcasterBase
    {
        private readonly IWalletManager walletManager;
        private readonly IConnectionManager connectionManager;
        private readonly Network network;
        private readonly ChainIndexer chainIndexer;
        private ILogger logger;

        public PeerStatisticsClientBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IConnectionManager connectionManager,
            ISignals signals,
            ChainIndexer chainIndexer,
            INodeLifetime nodeLifetime,
            EventsHub eventsHub)
            : base(eventsHub, signals, nodeLifetime, loggerFactory)
        {
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.chainIndexer = chainIndexer;
        }

        protected override IEnumerable<IClientEvent> GetMessages()
        {
            WalletGeneralInfoClientEvent clientEvent = null;
            try
            {
                foreach (string walletName in this.walletManager.GetWalletsNames())
                {
                    Wallet.Wallet wallet = this.walletManager.GetWallet(walletName);

                    clientEvent = new WalletGeneralInfoClientEvent
                    {
                        WalletName = walletName,
                        Network = wallet.Network,
                        CreationTime = wallet.CreationTime,
                        LastBlockSyncedHeight = wallet.AccountsRoot.Single().LastBlockSyncedHeight,
                        ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                        ChainTip = this.chainIndexer.Tip.Height,
                        IsChainSynced = this.chainIndexer.IsDownloaded(),
                        IsDecrypted = true
                    };

                    // Get the wallet's file path.
                    (string folder, IEnumerable<string> fileNameCollection) = this.walletManager.GetWalletsFiles();
                    string searchFile = Path.ChangeExtension(walletName, this.walletManager.GetWalletFileExtension());
                    string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                    if (folder != null && fileName != null)
                        clientEvent.WalletFilePath = Path.Combine(folder, fileName);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception occurred: {0}", e.StackTrace);
            }

            yield return clientEvent;
        }
    }
}