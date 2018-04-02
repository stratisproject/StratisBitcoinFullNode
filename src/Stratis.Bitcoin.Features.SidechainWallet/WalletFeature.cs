using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.SidechainWallet
{
    /// <inheritdoc />
    public class WalletFeature : Wallet.WalletFeature
    {
        public WalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, Signals.Signals signals, ConcurrentChain chain, IConnectionManager connectionManager, BroadcasterBehavior broadcasterBehavior, NodeSettings nodeSettings, WalletSettings walletSettings) 
            : base(walletSyncManager, walletManager, signals, chain, connectionManager, broadcasterBehavior, nodeSettings, walletSettings)
        {
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainWalletExtension
    {
        public static IFullNodeBuilder UseSidechainWallet(this IFullNodeBuilder fullNodeBuilder, Action<WalletSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>("sidechain-wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<SidechainWallet.WalletFeature>()
                //.DependOn<Wallet.WalletFeature>()
                .DependOn<MempoolFeature>()
                .DependOn<BlockStoreFeature>()
                .DependOn<RPCFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                    services.AddSingleton<IWalletTransactionHandler, SidechainWallet.WalletTransactionHandler>();
                    services.AddSingleton<IWalletManager, WalletManager>();
                    services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                    services.AddSingleton<SidechainWallet.Controllers.SidechainWalletController>();
                    services.AddSingleton<WalletRPCController>();
                    services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    services.AddSingleton<BroadcasterBehavior>();
                    services.AddSingleton<WalletSettings>(new WalletSettings(setup));
                });
            });

            return fullNodeBuilder;
        }
    }
}

