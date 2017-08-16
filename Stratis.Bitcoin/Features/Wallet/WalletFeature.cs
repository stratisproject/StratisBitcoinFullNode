using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using System;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletFeature : FullNodeFeature
    {
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IWalletManager walletManager;
        private readonly Signals.Signals signals;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;

        public WalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, Signals.Signals signals)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.signals = signals;
        }

        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletSyncManager));
            this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

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

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
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
                        services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                        services.AddSingleton<WalletController>();
                        services.AddSingleton<WalletRPCController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
