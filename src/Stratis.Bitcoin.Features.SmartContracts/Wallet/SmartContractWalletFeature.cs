﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public sealed class SmartContractWalletFeature : FullNodeFeature
    {
        private readonly BroadcasterBehavior broadcasterBehavior;
        private readonly ChainIndexer chainIndexer;
        private readonly IConnectionManager connectionManager;
        private readonly ILogger logger;
        private readonly IWalletManager walletManager;
        private readonly IWalletSyncManager walletSyncManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="WalletFeature"/> class.
        /// </summary>
        /// <param name="broadcasterBehavior">The broadcaster behavior.</param>
        /// <param name="chainIndexer">The chain of blocks.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
        public SmartContractWalletFeature(
            BroadcasterBehavior broadcasterBehavior,
            ChainIndexer chainIndexer,
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletSyncManager walletSyncManager,
            INodeStats nodeStats)
        {
            this.broadcasterBehavior = broadcasterBehavior;
            this.chainIndexer = chainIndexer;
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().Name);
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, this.GetType().Name);
        }

        private void AddComponentStats(StringBuilder log)
        {
            IEnumerable<string> walletNames = this.walletManager.GetWalletsNames();

            if (walletNames.Any())
            {
                log.AppendLine();
                log.AppendLine("======Wallets======");

                foreach (string walletName in walletNames)
                {
                    IEnumerable<UnspentOutputReference> items = this.walletManager.GetSpendableTransactionsInWallet(walletName, 1);
                    log.AppendLine("Wallet[SC]: " + (walletName + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
                }
            }
        }

        private void AddInlineStats(StringBuilder log)
        {
            if (this.walletManager is WalletManager walletManager)
            {
                int height = walletManager.LastBlockHeight();
                ChainedHeader block = this.chainIndexer.GetHeader(height);
                uint256 hashBlock = block == null ? 0 : block.HashBlock;

                log.AppendLine("Wallet[SC].Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                        (walletManager.ContainsWallets ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                        (walletManager.ContainsWallets ? (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
            }
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            this.walletManager.Start();
            this.walletSyncManager.Start();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            this.logger.LogInformation("Smart Contract Feature Wallet Injected.");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.walletManager.Stop();
            this.walletSyncManager.Stop();
        }
    }

    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseSmartContractWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>("smart contract wallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<SmartContractWalletFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                    services.AddSingleton<IWalletTransactionHandler, SmartContractWalletTransactionHandler>();
                    services.AddSingleton<IWalletManager, WalletManager>();
                    services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                    services.AddSingleton<ISmartContractTransactionService, SmartContractTransactionService>();
                    services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    services.AddSingleton<BroadcasterBehavior>();
                    services.AddSingleton<WalletSettings>();
                });
            });

            return fullNodeBuilder;
        }
    }
}