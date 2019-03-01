using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Notifications;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.RestClients;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;

[assembly: InternalsVisibleTo("Stratis.FederatedPeg.Features.FederationGateway.Tests")]
[assembly: InternalsVisibleTo("Stratis.FederatedPeg.IntegrationTests")]

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.Features.FederatedPeg
{
    internal class FederationGatewayFeature : FullNodeFeature
    {
        public const string FederationGatewayFeatureNamespace = "federationgateway";

        private readonly Signals signals;

        private readonly IDepositExtractor depositExtractor;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        private IDisposable blockSubscriberDisposable;

        private IDisposable transactionSubscriberDisposable;

        private readonly IConnectionManager connectionManager;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IFullNode fullNode;

        private readonly ILoggerFactory loggerFactory;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly ConcurrentChain chain;

        private readonly Network network;

        private readonly ICrossChainTransferStore crossChainTransferStore;

        private readonly IPartialTransactionRequester partialTransactionRequester;

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly MempoolManager mempoolManager;

        private readonly IMaturedBlocksSyncManager maturedBlocksSyncManager;

        private readonly IWithdrawalHistoryProvider withdrawalHistoryProvider;

        private readonly ILogger logger;

        public FederationGatewayFeature(
            ILoggerFactory loggerFactory,
            Signals signals,
            IDepositExtractor depositExtractor,
            IWithdrawalExtractor withdrawalExtractor,
            IWithdrawalReceiver withdrawalReceiver,
            IConnectionManager connectionManager,
            IFederationGatewaySettings federationGatewaySettings,
            IFullNode fullNode,
            IFederationWalletManager federationWalletManager,
            IFederationWalletSyncManager walletSyncManager,
            Network network,
            ConcurrentChain chain,
            INodeStats nodeStats,
            ICrossChainTransferStore crossChainTransferStore,
            IPartialTransactionRequester partialTransactionRequester,
            IFederationGatewayClient federationGatewayClient,
            MempoolManager mempoolManager,
            IMaturedBlocksSyncManager maturedBlocksSyncManager,
            IWithdrawalHistoryProvider withdrawalHistoryProvider)
        {
            this.loggerFactory = loggerFactory;
            this.signals = signals;
            this.depositExtractor = depositExtractor;
            this.withdrawalExtractor = withdrawalExtractor;
            this.withdrawalReceiver = withdrawalReceiver;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.fullNode = fullNode;
            this.chain = chain;
            this.federationWalletManager = federationWalletManager;
            this.walletSyncManager = walletSyncManager;
            this.network = network;
            this.crossChainTransferStore = crossChainTransferStore;
            this.partialTransactionRequester = partialTransactionRequester;
            this.federationGatewayClient = federationGatewayClient;
            this.mempoolManager = mempoolManager;
            this.maturedBlocksSyncManager = maturedBlocksSyncManager;
            this.withdrawalHistoryProvider = withdrawalHistoryProvider;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            // add our payload
            var payloadProvider = (PayloadProvider)this.fullNode.Services.ServiceProvider.GetService(typeof(PayloadProvider));
            payloadProvider.AddPayload(typeof(RequestPartialTransactionPayload));

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }

        public override Task InitializeAsync()
        {
            this.crossChainTransferStore.Initialize();

            // maturedBlocksSyncManager should be initialized only after crossChainTransferStore.
            this.maturedBlocksSyncManager.Initialize();

            this.federationWalletManager.Start();
            this.walletSyncManager.Start();
            this.crossChainTransferStore.Start();
            this.partialTransactionRequester.Start();

            // Connect the node to the other federation members.
            foreach (IPEndPoint federationMemberIp in this.federationGatewaySettings.FederationNodeIpEndPoints)
                this.connectionManager.AddNodeAddress(federationMemberIp);

            NetworkPeerConnectionParameters networkPeerConnectionParameters = this.connectionManager.Parameters;
            networkPeerConnectionParameters.TemplateBehaviors.Add(new PartialTransactionsBehavior(this.loggerFactory, this.federationWalletManager,
                this.network, this.federationGatewaySettings, this.crossChainTransferStore));

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();

            // Sync manager has to be disposed BEFORE cross chain transfer store.
            this.maturedBlocksSyncManager.Dispose();

            this.crossChainTransferStore.Dispose();
        }

        private void AddInlineStats(StringBuilder benchLogs)
        {
            if (this.federationWalletManager == null)
                return;

            int height = this.federationWalletManager.LastBlockHeight();
            ChainedHeader block = this.chain.GetBlock(height);
            uint256 hashBlock = block == null ? 0 : block.HashBlock;

            FederationWallet federationWallet = this.federationWalletManager.GetWallet();
            benchLogs.AppendLine("Fed. Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                 (federationWallet != null ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                 (federationWallet != null ? (" Fed. Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
        }

        private void AddComponentStats(StringBuilder benchLog)
        {
            try
            {
                string stats = this.CollectStats();
                benchLog.Append(stats);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                throw;
            }
        }

        private string CollectStats()
        {
            StringBuilder benchLog = new StringBuilder();
            benchLog.AppendLine();
            benchLog.AppendLine("====== Federation Wallet ======");

            (Money ConfirmedAmount, Money UnConfirmedAmount) balances = this.federationWalletManager.GetWallet().GetSpendableAmount();
            bool isFederationActive = this.federationWalletManager.IsFederationActive();
            benchLog.AppendLine("Federation Wallet: ".PadRight(LoggingConfiguration.ColumnLength)
                                + " Confirmed balance: " + balances.ConfirmedAmount.ToString().PadRight(LoggingConfiguration.ColumnLength)
                                + " Unconfirmed balance: " + balances.UnConfirmedAmount.ToString().PadRight(LoggingConfiguration.ColumnLength)
                                + " Federation Status: " + (isFederationActive ? "Active" : "Inactive"));
            benchLog.AppendLine();

            if (!isFederationActive)
            {
                var apiSettings = (ApiSettings)this.fullNode.Services.ServiceProvider.GetService(typeof(ApiSettings));

                benchLog.AppendLine("".PadRight(59, '=') + " W A R N I N G " + "".PadRight(59, '='));
                benchLog.AppendLine();
                benchLog.AppendLine("This federation node is not enabled. You will not be able to store or participate in signing of transactions until you enable it.");
                benchLog.AppendLine("If not done previously, please enable your federation node using " + $"{apiSettings.ApiUri}/api/FederationWallet/{FederationWalletRouteEndPoint.EnableFederation}.");
                benchLog.AppendLine();
                benchLog.AppendLine("".PadRight(133, '='));
                benchLog.AppendLine();
            }

            // Display recent withdrawals (if any).
            List<WithdrawalModel> withdrawals = this.withdrawalHistoryProvider.GetHistory(5);
            if (withdrawals.Count > 0)
            {
                benchLog.AppendLine("--- Recent Withdrawals ---");
                foreach (WithdrawalModel withdrawal in withdrawals)
                    benchLog.AppendLine(withdrawal.ToString());
                benchLog.AppendLine();
            }

            benchLog.AppendLine("====== NodeStore ======");
            this.AddBenchmarkLine(benchLog, new (string, int)[] {
                ("Height:", LoggingConfiguration.ColumnLength),
                (this.crossChainTransferStore.TipHashAndHeight.Height.ToString(), LoggingConfiguration.ColumnLength),
                ("Hash:",LoggingConfiguration.ColumnLength),
                (this.crossChainTransferStore.TipHashAndHeight.HashBlock.ToString(), 0),
                ("NextDepositHeight:", LoggingConfiguration.ColumnLength),
                (this.crossChainTransferStore.NextMatureDepositHeight.ToString(), LoggingConfiguration.ColumnLength),
                ("HasSuspended:",LoggingConfiguration.ColumnLength),
                (this.crossChainTransferStore.HasSuspended().ToString(), 0)
            },
            4);

            AddBenchmarkLine(benchLog,
                this.crossChainTransferStore.GetCrossChainTransferStatusCounter().SelectMany(item => new (string, int)[]{
                    (item.Key.ToString()+":", LoggingConfiguration.ColumnLength),
                    (item.Value.ToString(), LoggingConfiguration.ColumnLength)
                    }).ToArray(),
                4);

            benchLog.AppendLine();
            return benchLog.ToString();
        }

        private void AddBenchmarkLine(StringBuilder benchLog, (string Value, int ValuePadding)[] items, int maxItemsPerLine = int.MaxValue)
        {
            if (items != null)
            {
                int itemsAdded = 0;
                foreach (var item in items)
                {
                    if (itemsAdded++ >= maxItemsPerLine)
                    {
                        benchLog.AppendLine();
                        itemsAdded = 1;
                    }
                    benchLog.Append(item.Value.PadRight(item.ValuePadding));
                }
                benchLog.AppendLine();
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddFederationGateway(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<FederationGatewayFeature>(
                FederationGatewayFeature.FederationGatewayFeatureNamespace);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<FederationGatewayFeature>().DependOn<BlockNotificationFeature>().FeatureServices(
                    services =>
                    {
                        services.AddSingleton<IHttpClientFactory, HttpClientFactory>();
                        services.AddSingleton<IMaturedBlocksProvider, MaturedBlocksProvider>();
                        services.AddSingleton<IFederationGatewaySettings, FederationGatewaySettings>();
                        services.AddSingleton<IOpReturnDataReader, OpReturnDataReader>();
                        services.AddSingleton<IDepositExtractor, DepositExtractor>();
                        services.AddSingleton<IWithdrawalExtractor, WithdrawalExtractor>();
                        services.AddSingleton<IWithdrawalReceiver, WithdrawalReceiver>();
                        services.AddSingleton<FederationGatewayController>();
                        services.AddSingleton<IFederationWalletSyncManager, FederationWalletSyncManager>();
                        services.AddSingleton<IFederationWalletTransactionHandler, FederationWalletTransactionHandler>();
                        services.AddSingleton<IFederationWalletManager, FederationWalletManager>();
                        services.AddSingleton<IWithdrawalTransactionBuilder, WithdrawalTransactionBuilder>();
                        services.AddSingleton<ILeaderProvider, LeaderProvider>();
                        services.AddSingleton<FederationWalletController>();
                        services.AddSingleton<ICrossChainTransferStore, CrossChainTransferStore>();
                        services.AddSingleton<ILeaderReceiver, LeaderReceiver>();
                        services.AddSingleton<ISignedMultisigTransactionBroadcaster, SignedMultisigTransactionBroadcaster>();
                        services.AddSingleton<IPartialTransactionRequester, PartialTransactionRequester>();
                        services.AddSingleton<IFederationGatewayClient, FederationGatewayClient>();
                        services.AddSingleton<IMaturedBlocksSyncManager, MaturedBlocksSyncManager>();
                        services.AddSingleton<IWithdrawalHistoryProvider, WithdrawalHistoryProvider>();

                        // Set up events.
                        services.AddSingleton<TransactionObserver>();
                        services.AddSingleton<BlockObserver>();
                    });
            });
            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseFederatedPegPoAMining(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<PoAFeature>().DependOn<FederationGatewayFeature>().FeatureServices(services =>
                    {
                        services.AddSingleton<FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<SlotsManager>();
                        services.AddSingleton<BlockDefinition, FederatedPegBlockDefinition>();
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();
                    });
            });

            // TODO: Consensus and Mining should be separated. Sidechain nodes don't need any of the Federation code but do need Consensus.
            // In the dependency tree as it is currently however, consensus is dependent on PoAFeature (needs SlotManager) which is in turn dependent on
            // FederationGatewayFeature. https://github.com/stratisproject/FederatedSidechains/issues/273

            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<ConsensusFeature>().FeatureServices(services =>
                {
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<ICoinView, CachedCoinView>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<IConsensusRuleEngine, SmartContractPoARuleEngine>();
                    services.AddSingleton<IChainState, ChainState>();
                    services.AddSingleton<ConsensusQuery>()
                        .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                        .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                    new SmartContractPoARuleRegistration(fullNodeBuilder.Network).RegisterRules(fullNodeBuilder.Network.Consensus);
                });
            });

            return fullNodeBuilder;
        }
    }

    //todo: this should be removed when compatible with full node API, instead, we should use
    //services.AddHttpClient from Microsoft.Extensions.Http
    public class HttpClientFactory : IHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }
    }
}

