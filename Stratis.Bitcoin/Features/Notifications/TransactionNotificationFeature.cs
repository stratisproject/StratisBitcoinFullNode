using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Feature enabling the broadcasting of transactions.
    /// </summary>
    public class TransactionNotificationFeature : FullNodeFeature
    {
        private readonly IConnectionManager connectionManager;
        private readonly TransactionReceiver transactionBehavior;

        public TransactionNotificationFeature(IConnectionManager connectionManager, TransactionReceiver transactionBehavior)
        {
            this.connectionManager = connectionManager;
            this.transactionBehavior = transactionBehavior;
        }

        public override void Start()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(this.transactionBehavior);
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
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
                    });
            });

            return fullNodeBuilder;
        }
    }
}
