using System;
using Stratis.Bitcoin.Wallet.Controllers;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Wallet.Notifications;

namespace Stratis.Bitcoin.Wallet
{
    public class WalletFeature : FullNodeFeature
    {
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IWalletManager walletManager;
        private readonly Signals signals;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;

        public WalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, Signals signals)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.signals = signals;
        }

        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = new BlockSubscriber(this.signals.Blocks, new BlockObserver(this.walletSyncManager)).Subscribe();
            this.transactionSubscriberdDisposable = new TransactionSubscriber(this.signals.Transactions, new TransactionObserver(this.walletSyncManager)).Subscribe();

            this.walletManager.Initialize();
            this.walletSyncManager.Initialize();
        }

        public override void Stop()
        {
            this.blockSubscriberdDisposable.Dispose();
            this.transactionSubscriberdDisposable.Dispose();

            this.walletManager.Dispose();
        }
    }

    public static class WalletFeatureExtension
    {
        public static IFullNodeBuilder UseWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<WalletFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<WalletController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
