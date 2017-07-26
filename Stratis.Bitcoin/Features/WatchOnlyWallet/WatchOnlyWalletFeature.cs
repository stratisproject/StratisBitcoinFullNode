﻿using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Notifications;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    public class WatchOnlyWalletFeature : FullNodeFeature
    {
        private readonly IWatchOnlyWalletManager walletManager;
        private readonly Signals.Signals signals;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;

        public WatchOnlyWalletFeature(IWatchOnlyWalletManager walletManager, Signals.Signals signals, ConcurrentChain chain)
        {
            this.walletManager = walletManager;
            this.signals = signals;
        }

        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = this.signals.Blocks.Subscribe(new BlockObserver(this.walletManager));
            this.transactionSubscriberdDisposable = this.signals.Transactions.Subscribe(new TransactionObserver(this.walletManager));

            this.walletManager.Initialize();
        }

        public override void Stop()
        {
            this.blockSubscriberdDisposable.Dispose();
            this.transactionSubscriberdDisposable.Dispose();

            this.walletManager.Dispose();
        }
    }

    public static class WatchOnlyWalletFeatureExtension
    {
        public static IFullNodeBuilder UseWatchOnlyWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<WatchOnlyWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWatchOnlyWalletManager, WatchOnlyWalletManager>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
