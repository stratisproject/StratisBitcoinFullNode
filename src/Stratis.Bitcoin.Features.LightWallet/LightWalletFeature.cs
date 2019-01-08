using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.Pruning;
using Stratis.Bitcoin.Features.LightWallet.Broadcasting;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Feature for a full-block SPV wallet.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    public class LightWalletFeature : FullNodeFeature
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private readonly IWalletSyncManager walletSyncManager;

        private readonly IWalletManager walletManager;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        private readonly NodeDeployments nodeDeployments;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly IWalletFeePolicy walletFeePolicy;

        private readonly BroadcasterBehavior broadcasterBehavior;

        private readonly StoreSettings storeSettings;

        private readonly WalletSettings walletSettings;

        private readonly IPruneBlockStoreService lightWalletBlockStoreService;

        public LightWalletFeature(
            IWalletSyncManager walletSyncManager,
            IWalletManager walletManager,
            IConnectionManager connectionManager,
            ConcurrentChain chain,
            NodeDeployments nodeDeployments,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            IWalletFeePolicy walletFeePolicy,
            BroadcasterBehavior broadcasterBehavior,
            ILoggerFactory loggerFactory,
            StoreSettings storeSettings,
            WalletSettings walletSettings,
            INodeStats nodeStats,
            IPruneBlockStoreService lightWalletBlockStoreService)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.nodeDeployments = nodeDeployments;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.walletFeePolicy = walletFeePolicy;
            this.broadcasterBehavior = broadcasterBehavior;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.storeSettings = storeSettings;
            this.walletSettings = walletSettings;

            this.lightWalletBlockStoreService = lightWalletBlockStoreService;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            WalletSettings.PrintHelp(network);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(new DropNodesBehaviour(this.chain, this.connectionManager, this.loggerFactory));
            this.walletSettings.IsLightWallet = true;

            this.walletManager.Start();
            this.walletSyncManager.Start();

            this.asyncLoop = this.StartDeploymentsChecksLoop();

            this.walletFeePolicy.Start();

            if (this.storeSettings.PruningEnabled)
                this.lightWalletBlockStoreService.Initialize();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
            return Task.CompletedTask;
        }

        public IAsyncLoop StartDeploymentsChecksLoop()
        {
            CancellationTokenSource loopToken = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping);
            return this.asyncLoopFactory.Run("LightWalletFeature.CheckDeployments", token =>
            {
                if (!this.chain.IsDownloaded())
                    return Task.CompletedTask;

                // check segwit activation on the chain of headers
                // if segwit is active signal to only connect to
                // nodes that also signal they are segwit nodes
                DeploymentFlags flags = this.nodeDeployments.GetFlags(this.walletSyncManager.WalletTip);
                if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                    this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

                // done checking
                loopToken.Cancel();

                return Task.CompletedTask;
            },
            loopToken.Token,
            repeatEvery: TimeSpans.TenSeconds,
            startAfter: TimeSpans.TenSeconds);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.walletFeePolicy.Stop();
            this.asyncLoop.Dispose();
            this.walletSyncManager.Stop();
            this.walletManager.Stop();

            this.lightWalletBlockStoreService?.Dispose();
        }

        public void AddInlineStats(StringBuilder log)
        {
            if (this.walletManager is WalletManager manager)
            {
                int height = manager.LastBlockHeight();
                ChainedHeader block = this.chain.GetBlock(height);
                uint256 hashBlock = block == null ? 0 : block.HashBlock;

                log.AppendLine("LightWallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                        (manager.ContainsWallets ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                        (manager.ContainsWallets ? (" LightWallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));

                if (this.storeSettings.PruningEnabled)
                    log.AppendLine("LightWallet.Pruned:".PadRight(LoggingConfiguration.ColumnLength + 1) + this.lightWalletBlockStoreService.PrunedUpToHeaderTip?.Height.ToString().PadRight(8));
            }
        }

        public void AddComponentStats(StringBuilder log)
        {
            IEnumerable<string> walletNames = this.walletManager.GetWalletsNames();

            if (walletNames.Any())
            {
                log.AppendLine();
                log.AppendLine("======Wallets======");

                foreach (string walletName in walletNames)
                {
                    IEnumerable<UnspentOutputReference> items = this.walletManager.GetSpendableTransactionsInWallet(walletName, 1);
                    log.AppendLine("Wallet: " + (walletName + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
                }
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderLightWalletExtension
    {
        public static IFullNodeBuilder UseLightWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<LightWalletFeature>()
                    .DependOn<TransactionNotificationFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, LightWalletSyncManager>();
                        services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        if (fullNodeBuilder.Network.IsBitcoin())
                            services.AddSingleton<IWalletFeePolicy, LightWalletBitcoinExternalFeePolicy>();
                        else
                            services.AddSingleton<IWalletFeePolicy, LightWalletFixedFeePolicy>();
                        services.AddSingleton<WalletController>();
                        services.AddSingleton<IBroadcasterManager, LightWalletBroadcasterManager>();
                        services.AddSingleton<BroadcasterBehavior>();
                        services.AddSingleton<IInitialBlockDownloadState, LightWalletInitialBlockDownloadState>();
                        services.AddSingleton<WalletSettings>();
                        services.AddSingleton<IScriptAddressReader, ScriptAddressReader>();
                        services.AddSingleton<StandardTransactionPolicy>();

                        services.AddSingleton<IPruneBlockStoreService, PruneBlockStoreService>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
