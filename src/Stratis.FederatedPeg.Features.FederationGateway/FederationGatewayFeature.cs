using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.MonitorChain;
using Stratis.FederatedPeg.Features.FederationGateway.Notifications;

[assembly: InternalsVisibleTo("Stratis.FederatedPeg.Features.FederationGateway.Tests")]
[assembly: InternalsVisibleTo("Stratis.FederatedPeg.IntegrationTests")]

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class FederationGatewayFeature : FullNodeFeature
    {
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly Signals signals;

        private IDisposable blockSubscriberDisposable;

        private readonly IConnectionManager connectionManager;

        private FederationGatewaySettings federationGatewaySettings;

        private NodeSettings nodeSettings;

        private IFullNode fullNode;

        private ILoggerFactory loggerFactory;

        private IGeneralPurposeWalletManager generalPurposeWalletManager;

        private Network network;

        private IMonitorChainSessionManager monitorChainSessionManager;
        
        private ICounterChainSessionManager counterChainSessionManager;

        public FederationGatewayFeature(ILoggerFactory loggerFactory, ICrossChainTransactionMonitor crossChainTransactionMonitor, Signals signals,
            IConnectionManager connectionManager,
            FederationGatewaySettings federationGatewaySettings, NodeSettings nodeSettings, IFullNode fullNode,
            IGeneralPurposeWalletManager generalPurposeWalletManager, Network network,
            IMonitorChainSessionManager monitorChainSessionManager, ICounterChainSessionManager counterChainSessionManager)
        {
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.signals = signals;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.nodeSettings = nodeSettings;
            this.fullNode = fullNode;
            this.generalPurposeWalletManager = generalPurposeWalletManager;
            this.network = network;

            this.counterChainSessionManager = counterChainSessionManager;
            this.monitorChainSessionManager = monitorChainSessionManager;

            // add our payload
            var payloadProvider = this.fullNode.Services.ServiceProvider.GetService(typeof(PayloadProvider)) as PayloadProvider;
            payloadProvider.AddPayload(typeof(RequestPartialTransactionPayload));
        }

        public override void Initialize()
        {
            // subscribe to receiving transactions
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.crossChainTransactionMonitor));

            this.crossChainTransactionMonitor.Initialize(federationGatewaySettings);
            this.monitorChainSessionManager.Initialize();

            var networkPeerConnectionParameters = this.connectionManager.Parameters;
            networkPeerConnectionParameters.TemplateBehaviors.Add(new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.generalPurposeWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings ));
        }

        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.crossChainTransactionMonitor.Dispose();
            this.monitorChainSessionManager.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddFederationGateway(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FederationGatewayFeature>()
                    .DependOn<BlockNotificationFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<FederationGatewayController>();
                        services.AddSingleton<FederationGatewaySettings>();
                        services.AddSingleton<ICrossChainTransactionMonitor, CrossChainTransactionMonitor>();
                        services.AddSingleton<ICrossChainTransactionAuditor, JsonCrossChainTransactionAuditor>();
                        services.AddSingleton<IMonitorChainSessionManager, MonitorChainSessionManager>();
                        services.AddSingleton<ICounterChainSessionManager, CounterChainSessionManager>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
