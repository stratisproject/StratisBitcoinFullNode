using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Controllers;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Notifications;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// A feature used to add a watch-only wallet to the full node.
    /// </summary>
    public class WatchOnlyWalletFeature : FullNodeFeature
    {
        private readonly IWatchOnlyWalletManager walletManager;
        private readonly Signals.Signals signals;

        private IDisposable blockSubscriberdDisposable;
        private IDisposable transactionSubscriberdDisposable;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatchOnlyWalletFeature"/> class.
        /// </summary>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="signals">The signals.</param>
        public WatchOnlyWalletFeature(IWatchOnlyWalletManager walletManager, Signals.Signals signals)
        {
            this.walletManager = walletManager;
            this.signals = signals;
        }

        /// <inheritdoc />
        public override void Start()
        {
            // subscribe to receiving blocks and transactions
            this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletManager));
            this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletManager));

            this.walletManager.Initialize();
        }

        /// <inheritdoc />
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
        /// <summary>
        /// Adds a watch only wallet component to the node being initialized.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder UseWatchOnlyWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<WatchOnlyWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWatchOnlyWalletManager, WatchOnlyWalletManager>();
                        services.AddSingleton<WatchOnlyWalletController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
