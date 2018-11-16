using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Notifications;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using BlockObserver = Stratis.FederatedPeg.Features.FederationGateway.Notifications.BlockObserver;

[assembly: InternalsVisibleTo("Stratis.FederatedPeg.Features.FederationGateway.Tests")]
[assembly: InternalsVisibleTo("Stratis.FederatedPeg.IntegrationTests")]

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class FederationGatewayFeature : FullNodeFeature
    {
        internal const string JsonHttpClientName = "jsonClient";

        public const string FederationGatewayFeatureNamespace = "federationgateway";

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IBlockTipSender blockTipSender;

        private readonly Signals signals;

        private readonly IDepositExtractor depositExtractor;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        private readonly ILeaderProvider leaderProvider;

        private IDisposable blockSubscriberDisposable;

        private IDisposable transactionSubscriberDisposable;

        private readonly IConnectionManager connectionManager;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private IFullNode fullNode;

        private readonly ILoggerFactory loggerFactory;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly ConcurrentChain chain;

        private readonly Network network;

        private readonly ICrossChainTransferStore crossChainTransferStore;

        public FederationGatewayFeature(
            ILoggerFactory loggerFactory,
            IMaturedBlockReceiver maturedBlockReceiver,
            IMaturedBlockSender maturedBlockSender,
            IBlockTipSender blockTipSender,
            Signals signals,
            IDepositExtractor depositExtractor,
            IWithdrawalExtractor withdrawalExtractor,
            IWithdrawalReceiver withdrawalReceiver,
            ILeaderProvider leaderProvider,
            IConnectionManager connectionManager,
            IFederationGatewaySettings federationGatewaySettings,
            IFullNode fullNode,
            IFederationWalletManager federationWalletManager,
            IFederationWalletSyncManager walletSyncManager,
            Network network,
            ConcurrentChain chain,
            INodeStats nodeStats,
            ICrossChainTransferStore crossChainTransferStore)
        {
            this.loggerFactory = loggerFactory;
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.maturedBlockSender = maturedBlockSender;
            this.blockTipSender = blockTipSender;
            this.signals = signals;
            this.depositExtractor = depositExtractor;
            this.withdrawalExtractor = withdrawalExtractor;
            this.withdrawalReceiver = withdrawalReceiver;
            this.leaderProvider = leaderProvider;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.fullNode = fullNode;
            this.chain = chain;
            this.federationWalletManager = federationWalletManager;
            this.walletSyncManager = walletSyncManager;
            this.network = network;
            this.crossChainTransferStore = crossChainTransferStore;

            // add our payload
            var payloadProvider = (PayloadProvider)this.fullNode.Services.ServiceProvider.GetService(typeof(PayloadProvider));
            payloadProvider.AddPayload(typeof(RequestPartialTransactionPayload));

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }

        public override async Task InitializeAsync()
        {
            // Subscribe to receiving blocks and transactions.
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocksConnected(
                new BlockObserver(
                    this.walletSyncManager,
                    this.depositExtractor,
                    this.withdrawalExtractor,
                    this.withdrawalReceiver,
                    this.maturedBlockSender,
                    this.blockTipSender));

            this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

            this.crossChainTransferStore.Initialize();

            this.federationWalletManager.Start();
            this.walletSyncManager.Start();
            this.crossChainTransferStore.Start();

            // Connect the node to the other federation members.
            foreach (var federationMemberIp in this.federationGatewaySettings.FederationNodeIpEndPoints)
            {
                this.connectionManager.AddNodeAddress(federationMemberIp);
            }

            var networkPeerConnectionParameters = this.connectionManager.Parameters;
            networkPeerConnectionParameters.TemplateBehaviors.Add(new PartialTransactionsBehavior(this.loggerFactory, this.federationWalletManager, this.network, this.federationGatewaySettings));
        }

        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();
            this.crossChainTransferStore.Dispose();
        }

        public void AddInlineStats(StringBuilder benchLogs)
        {
            if (this.federationWalletManager == null) return;
            int height = this.federationWalletManager.LastBlockHeight();
            ChainedHeader block = this.chain.GetBlock(height);
            uint256 hashBlock = block == null ? 0 : block.HashBlock;

            var federationWallet = this.federationWalletManager.GetWallet();
            benchLogs.AppendLine("Federation Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                 (federationWallet != null ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                 (federationWallet != null ? (" Federation Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : String.Empty));
        }

        public void AddComponentStats(StringBuilder benchLog)
        {
            benchLog.AppendLine();
            benchLog.AppendLine("====== Federation Wallet ======");

            var items = this.federationWalletManager.GetSpendableTransactionsInWallet(1);
            benchLog.AppendLine("Federation Wallet: ".PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
            benchLog.AppendLine();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddFederationGateway(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<FederationGatewayFeature>(FederationGatewayFeature.FederationGatewayFeatureNamespace);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FederationGatewayFeature>()
                    .DependOn<BlockNotificationFeature>()
                    .FeatureServices(services => {

                        services.AddSingleton<IHttpClientFactory, HttpClientFactory>();
                        services.AddSingleton<IMaturedBlockReceiver, MaturedBlockReceiver>();
                        services.AddSingleton<IMaturedBlockSender, RestMaturedBlockSender>();
                        services.AddSingleton<IBlockTipSender, RestBlockTipSender>();
                        services.AddSingleton<IFederationGatewaySettings, FederationGatewaySettings>();
                        services.AddSingleton<IOpReturnDataReader, OpReturnDataReader>();
                        services.AddSingleton<IDepositExtractor, DepositExtractor>();
                        services.AddSingleton<IWithdrawalExtractor, WithdrawalExtractor>();
                        services.AddSingleton<IWithdrawalReceiver, WithdrawalReceiver>();
                        services.AddSingleton<IEventPersister, EventsPersister>();
                        services.AddSingleton<FederationGatewayController>();
                        services.AddSingleton<IFederationWalletSyncManager, FederationWalletSyncManager>();
                        services.AddSingleton<IFederationWalletTransactionHandler, FederationWalletTransactionHandler>();
                        services.AddSingleton<IFederationWalletManager, FederationWalletManager>();
                        services.AddSingleton<ILeaderProvider, LeaderProvider>();
                        services.AddSingleton<FederationWalletController>();
                        services.AddSingleton<ICrossChainTransferStore, CrossChainTransferStore>();
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