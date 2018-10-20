//using System;
//using System.Threading.Tasks;
//using City.Chain.Features.SimpleWallet.Notifications;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Stratis.Bitcoin;
//using Stratis.Bitcoin.Builder;
//using Stratis.Bitcoin.Builder.Feature;
//using Stratis.Bitcoin.Features.Notifications;
//using Stratis.Bitcoin.Signals;

//namespace City.Chain.Features.SimpleWallet
//{
//    public class SimpleWalletFeature : FullNodeFeature
//    {
//        private readonly ILogger logger;

//        private readonly Stratis.Bitcoin.Signals.Signals signals;

//        private readonly SimpleWalletService walletService;

//        private readonly IFullNodeBuilder fullNodeBuilder;

//        private readonly FullNode fullNode;

//        private IDisposable blockSubscriberdDisposable;

//        private IDisposable transactionSubscriberdDisposable;

//        private IWebHost webHost;

//        private readonly SimpleWalletSettings walletSettings;

//        public SimpleWalletFeature(ILoggerFactory loggerFactory,
//            IFullNodeBuilder fullNodeBuilder,
//            Stratis.Bitcoin.Signals.Signals signals,
//            FullNode fullNode,
//            SimpleWalletSettings walletSettings,
//            SimpleWalletService walletService)
//        {
//            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
//            this.signals = signals;
//            this.walletService = walletService;
//            this.fullNode = fullNode;
//            this.fullNodeBuilder = fullNodeBuilder;
//            this.walletSettings = walletSettings;
//        }
        
//        public override Task InitializeAsync()
//        {
//            // subscribe to receiving blocks and transactions
//            //this.blockSubscriberdDisposable = this.signals.SubscribeForBlocksConnected(new SignalObserver(this.walletService));
//            //this.blockSubscriberdDisposable = this.signals.SubscribeForBlocksConnected(new SignalObserver(this.walletService));

//            //this.blockSubscriberdDisposable = this.signals.SubscribeForBlocksConnected(new BlockObserver(this.walletService));
//            //this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletService));

//            //this.walletManager.Initialize();
//            //this.fullNodeBuilder.Services, this.fullNode, this.apiSettings

//            this.webHost = Program.CreateWebHostBuilder(null).ConfigureServices(collection => {

//                // copies all the services defined for the full node to the Api.
//                // also copies over singleton instances already defined
//                foreach (ServiceDescriptor service in this.fullNodeBuilder.Services)
//                {
//                    object obj = this.fullNode.Services.ServiceProvider.GetService(service.ServiceType);

//                    if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
//                    {
//                        collection.AddSingleton(service.ServiceType, obj);
//                    }
//                    else
//                    {
//                        collection.Add(service);
//                    }
//                }

//            }).UseUrls(this.walletSettings.GetUrls()).Build();

//            this.webHost.Start();  //.Run();

//            return Task.CompletedTask;

//            //Program.CreateWebHostBuilder(null).ConfigureServices(collection => { }).Build().Run();
//            //this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.apiSettings);
//        }
//    }

//    /// <summary>
//    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
//    /// </summary>
//    public static class FullNodeBuilderSimpleWalletExtension
//    {
//        public static IFullNodeBuilder AddSimpleWallet(this IFullNodeBuilder fullNodeBuilder)
//        {
//            Stratis.Bitcoin.Configuration.Logging.LoggingConfiguration.RegisterFeatureNamespace<SimpleWalletFeature>("simplewallet");

//            fullNodeBuilder.ConfigureFeature(features =>
//            {
//                features
//                .AddFeature<SimpleWalletFeature>()
//                //.DependOn<BlockStoreFeature>()
//                .DependOn<BlockNotificationFeature>()
//                .DependOn<TransactionNotificationFeature>()
//                .FeatureServices(services =>
//                {
//                    services.AddSingleton(fullNodeBuilder); // We need this?
//                    services.AddSingleton<HubCommands>();
//                    services.AddTransient<SimpleWalletManager>();
//                    services.AddSingleton<SimpleWalletService>();
//                    services.AddSingleton<SimpleWalletSettings>();
//                });
//            });

//            return fullNodeBuilder;
//        }
//    }
//}
