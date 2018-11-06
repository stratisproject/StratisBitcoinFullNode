using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking.Controllers;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// Feature for cold staking which eliminates the need to keep the coins in the hot wallet.
    /// </summary>
    /// <remarks>
    /// <para>In order to produce blocks on Stratis network, a miner has to be online with running
    /// node and have its wallet open. This is necessary because at each time slot, the miner is
    /// supposed to check whether one of its UTXOs is eligible to be used as so-called coinstake kernel
    /// input and if so, it needs to use the private key associated with this UTXO in order to produce
    /// the coinstake transaction.</para>
    /// <para>The chance of a UTXO being eligible for producing a coinstake transaction grows linearly
    /// with the number of coins that the UTXO presents. This implies that the biggest miners on the
    /// network are required to keep the coins in a hot wallet. This is dangerous in case the machine
    /// where the hot wallet runs is compromised.</para>
    /// <para>Cold staking is a mechanism that eliminates the need to keep the coins in the hot wallet.
    /// With cold staking implemented, the miner still needs to be online and running a node with an open
    /// wallet, but the coins that are used for staking can be safely stored in cold storage. Therefore
    /// the open hot wallet does not need to hold any significant amount of coins, or it can even be
    /// completely empty.</para>
    /// </remarks>
    /// <seealso cref="ColdStakingManager.GetColdStakingScript(NBitcoin.ScriptId, NBitcoin.ScriptId)"/>
    /// <seealso cref="FullNodeFeature"/>
    public class ColdStakingFeature : BaseWalletFeature
    {
        /// <summary>The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</summary>
        private readonly IWalletSyncManager walletSyncManager;

        /// <summary>The signals responsible for receiving blocks and transactions from the network.</summary>
        private readonly Signals.Signals signals;

        /// <summary>Records disposable returned by <see cref="Signals.Signals.SubscribeForBlocksConnected"/> for <see cref="BlockObserver"/>.</summary>
        private IDisposable blockSubscriberDisposable;

        /// <summary>Records disposable returned by <see cref="Signals.Signals.SubscribeForBlocksConnected"/> for <see cref="TransactionObserver"/>.</summary>
        private IDisposable transactionSubscriberDisposable;

        /// <summary>The chain of blocks.</summary>
        private ConcurrentChain chain;

        /// <summary>The connection manager.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>The broadcaster behavior.</summary>
        private readonly BroadcasterBehavior broadcasterBehavior;

        /// <summary>The settings for the node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>The settings for the wallet.</summary>
        private readonly WalletSettings walletSettings;

        /// <summary>The logger factory used to create instance loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>The instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The cold staking manager.</summary>
        private readonly ColdStakingManager coldStakingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColdStakingFeature"/> class.
        /// </summary>
        /// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="signals">The signals responsible for receiving blocks and transactions from the network.</param>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="broadcasterBehavior">The broadcaster behavior.</param>
        /// <param name="nodeSettings">The settings for the node.</param>
        /// <param name="walletSettings">The settings for the wallet.</param>
        /// <param name="walletManager">The cold staking manager.</param>
        /// <param name="loggerFactory">The factory used to create instance loggers.</param>
        /// <param name="nodeStats">The node stats object used to register node stats.</param>
        public ColdStakingFeature(
            IWalletSyncManager walletSyncManager,
            IWalletManager walletManager,
            Signals.Signals signals,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            NodeSettings nodeSettings,
            WalletSettings walletSettings,
            ILoggerFactory loggerFactory,
            INodeStats nodeStats)
        {
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.coldStakingManager = walletManager as ColdStakingManager;
            Guard.NotNull(this.coldStakingManager, nameof(this.coldStakingManager));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;

            this.walletSyncManager = walletSyncManager;
            this.signals = signals;
            this.chain = chain;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;
            this.nodeSettings = nodeSettings;
            this.walletSettings = walletSettings;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            WalletSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            WalletSettings.BuildDefaultConfigurationFile(builder, network);
        }

        private void AddInlineStats(StringBuilder benchLogs)
        {
            var walletManager = this.coldStakingManager;

            if (walletManager != null)
            {
                int height = walletManager.LastBlockHeight();
                ChainedHeader block = this.chain.GetBlock(height);
                uint256 hashBlock = block == null ? 0 : block.HashBlock;

                benchLogs.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                        (walletManager.ContainsWallets ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                        (walletManager.ContainsWallets ? (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
            }
        }

        private void AddComponentStats(StringBuilder benchLog)
        {
            IEnumerable<string> walletNames = this.coldStakingManager.GetWalletsNames();

            if (walletNames.Any())
            {
                benchLog.AppendLine();
                benchLog.AppendLine("======Wallets======");

                foreach (string walletName in walletNames)
                {
                    IEnumerable<UnspentOutputReference> normalTransactions = this.coldStakingManager.GetSpendableTransactionsInWallet(walletName, 1);
                    IEnumerable<UnspentOutputReference> hotWalletTransactions = this.coldStakingManager.GetSpendableTransactionsInColdWallet(walletName, false, 1);
                    IEnumerable<UnspentOutputReference> coldWalletTransactons = this.coldStakingManager.GetSpendableTransactionsInColdWallet(walletName, true, 1);
                    benchLog.AppendLine("Wallet     : " + (walletName + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(normalTransactions.Sum(s => s.Transaction.Amount)).ToString() + " Cold stake hot balance: " + new Money(hotWalletTransactions.Sum(s => s.Transaction.Amount)).ToString() + " Cold stake cold balance: " + new Money(coldWalletTransactons.Sum(s => s.Transaction.Amount)).ToString());
                }
            }
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            // subscribe to receiving blocks and transactions
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocksConnected(new BlockObserver(this.walletSyncManager));
            this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

            this.coldStakingManager.Start();
            this.walletSyncManager.Start();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();

            this.coldStakingManager.Stop();
            this.walletSyncManager.Stop();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is not a Stratis network.</exception>
    public static class FullNodeBuilderColdStakingExtension
    {
        public static IFullNodeBuilder UseColdStakingWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            // Ensure that this feature is only used on a Stratis network.
            if (!fullNodeBuilder.Network.Consensus.IsProofOfStake)
                throw new InvalidOperationException("Cold staking can only be used on a Stratis network.");

            // Register the cold staking script template.
            StandardScripts.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);

            LoggingConfiguration.RegisterFeatureNamespace<ColdStakingFeature>("wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ColdStakingFeature>()
                .DependOn<MempoolFeature>()
                .DependOn<BlockStoreFeature>()
                .DependOn<RPCFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                    services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                    services.AddSingleton<IWalletManager, ColdStakingManager>();
                    services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                    services.AddSingleton<ColdStakingController>();
                    services.AddSingleton<WalletController>();
                    services.AddSingleton<WalletRPCController>();
                    services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    services.AddSingleton<BroadcasterBehavior>();
                    services.AddSingleton<WalletSettings>();
                    services.AddSingleton<IScriptAddressReader>(new ScriptAddressReader());
                    services.AddSingleton<StandardTransactionPolicy>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
