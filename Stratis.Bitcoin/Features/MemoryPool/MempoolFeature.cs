using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool.Fee;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolFeature : FullNodeFeature
    {
        private readonly Signals.Signals signals;
        private readonly IConnectionManager connectionManager;
        private readonly MempoolSignaled mempoolSignaled;
        private readonly MempoolBehavior mempoolBehavior;
        private readonly MempoolManager mempoolManager;
        private readonly ILogger mempoolLogger;

        public MempoolFeature(
            IConnectionManager connectionManager,
            Signals.Signals signals,
            MempoolSignaled mempoolSignaled,
            MempoolBehavior mempoolBehavior,
            MempoolManager mempoolManager,
            ILoggerFactory loggerFactory)
        {
            this.signals = signals;
            this.connectionManager = connectionManager;
            this.mempoolSignaled = mempoolSignaled;
            this.mempoolBehavior = mempoolBehavior;
            this.mempoolManager = mempoolManager;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Start()
        {
            this.mempoolManager.LoadPool().GetAwaiter().GetResult();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.mempoolBehavior);
            this.signals.SubscribeForBlocks(this.mempoolSignaled);
        }

        public override void Stop()
        {
            if (this.mempoolManager != null)
            {
                this.mempoolLogger.LogInformation("Saving Memory Pool...");

                MemPoolSaveResult result = this.mempoolManager.SavePool();
                if (result.Succeeded)
                {
                    this.mempoolLogger.LogInformation($"...Memory Pool Saved {result.TrxSaved} transactions");
                }
                else
                {
                    this.mempoolLogger.LogWarning("...Memory Pool Not Saved!");
                }
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseMempool(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<MempoolFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<MempoolScheduler>();
                    services.AddSingleton<TxMempool>();
                    services.AddSingleton<BlockPolicyEstimator>();
                    services.AddSingleton<FeeRate>(MempoolValidator.MinRelayTxFee);
                    services.AddSingleton<IMempoolValidator, MempoolValidator>();
                    services.AddSingleton<MempoolOrphans>();
                    services.AddSingleton<MempoolManager>();
                    services.AddSingleton<MempoolBehavior>();
                    services.AddSingleton<MempoolSignaled>();
                    services.AddSingleton<IMempoolPersistence, MempoolPersistence>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
