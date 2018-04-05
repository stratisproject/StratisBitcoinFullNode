using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.FederatedSidechainWallet
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
    public static class FullNodeBuilderFederatedSidechainWalletExtension
    {
        public static IFullNodeBuilder UseFederatedSidechainWallet(this IFullNodeBuilder fullNodeBuilder, Action<WalletSettings> setup = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>("sidechain-wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<FederatedSidechainWallet.WalletFeature>()
                .DependOn<Wallet.WalletFeature>()
                .DependOn<MempoolFeature>()
                .DependOn<BlockStoreFeature>()
                .DependOn<RPCFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                    services.AddSingleton<FederatedSidechainWallet.Interfaces.IWalletTransactionHandler, FederatedSidechainWallet.WalletTransactionHandler>();
                    services.AddSingleton<IWalletManager, WalletManager>();
                    services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                    services.AddSingleton<FederatedSidechainWallet.Controllers.FederatedSidechainWalletController>();
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

