using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Notifications
{
    /// <summary>
    /// Feature enabling the broadcasting of transactions.
    /// </summary>
    public class TransactionNotificationFeature : FullNodeFeature
    {
        private readonly ConnectionManager connectionManager;
        private readonly TransactionReceiver transactionBehavior;

        public TransactionNotificationFeature(ConnectionManager connectionManager, TransactionReceiver transactionBehavior)
        {
            this.connectionManager = connectionManager;
            this.transactionBehavior = transactionBehavior;
        }

        public override void Start()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(this.transactionBehavior);
        }
    }

    public static class TransactionNotificationFeatureExtension
    {
        public static IFullNodeBuilder UseTransactionNotification(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<TransactionNotificationFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<TransactionNotificationProgress>();
                        services.AddSingleton<TransactionNotification>();
                        services.AddSingleton<TransactionReceiver>();
                        services.AddSingleton<Signals>().AddSingleton<ISignals, Signals>(provider => provider.GetService<Signals>());
                    });
            });

            return fullNodeBuilder;
        }
    }
}
