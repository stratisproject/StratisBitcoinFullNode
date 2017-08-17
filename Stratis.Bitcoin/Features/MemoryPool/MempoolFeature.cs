using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool.Fee;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Transaction memory pool feature for the Full Node.
    /// </summary>
    /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/6dbcc74a0e0a7d45d20b03bb4eb41a027397a21d/src/txmempool.cpp"/>
    public class MempoolFeature : FullNodeFeature 
    {
        #region Fields

        /// <summary>Node notifications available to subscribe to.</summary>
        private readonly Signals.Signals signals;

        /// <summary>Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Observes block signal notifications from signals.</summary>
        private readonly MempoolSignaled mempoolSignaled;

        /// <summary>Memory pool node behavior for managing attached node messages.</summary>
        private readonly MempoolBehavior mempoolBehavior;

        /// <summary>Memory pool manager for managing external access to memory pool.</summary>
        private readonly MempoolManager mempoolManager;

        /// <summary>Logger for the memory pool component.</summary>
        private readonly ILogger mempoolLogger;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a memory pool feature.
        /// </summary>
        /// <param name="connectionManager">Connection manager for managing node connections.</param>
        /// <param name="signals">Node notifications available to subscribe to.</param>
        /// <param name="mempoolSignaled">Observes block signal notifications from signals.</param>
        /// <param name="mempoolBehavior">Memory pool node behavior for managing attached node messages.</param>
        /// <param name="mempoolManager">Memory pool manager for managing external access to memory pool.</param>
        /// <param name="loggerFactory">Logger factory for creating loggers.</param>
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

        #endregion

        #region FullNodeFeature Overrides

        /// <inheritdoc />
        public override void Start()
        {
            this.mempoolManager.LoadPool().GetAwaiter().GetResult();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.mempoolBehavior);
            this.signals.SubscribeForBlocks(this.mempoolSignaled);
        }

        /// <inheritdoc />
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

        #endregion
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Include the memory pool feature and related services in the full node.
        /// </summary>
        /// <param name="fullNodeBuilder">Full node builder.</param>
        /// <returns>Full node builder.</returns>
        public static IFullNodeBuilder UseMempool(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<MempoolFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<MempoolAsyncLock>();
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
