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
        private readonly ConcurrentChain chain;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;

        public WalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, Signals signals, ConcurrentChain chain)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.signals = signals;
            this.chain = chain;
        }

        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = new BlockSubscriber(this.signals.Blocks, new BlockObserver(this.chain, this.walletSyncManager)).Subscribe();
            this.transactionSubscriberdDisposable = new TransactionSubscriber(this.signals.Transactions, new TransactionObserver(this.walletSyncManager)).Subscribe();

            this.walletSyncManager.Initialize();
            this.walletManager.Initialize();
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
