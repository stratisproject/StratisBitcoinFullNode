using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolFeature : FullNodeFeature 
	{
		private readonly Signals signals;
		private readonly ConnectionManager connectionManager;
		private readonly MempoolSignaled mempoolSignaled;
		private readonly MempoolBehavior mempoolBehavior;
		private readonly MempoolManager mempoolManager;

		public MempoolFeature(ConnectionManager connectionManager, Signals signals, MempoolSignaled mempoolSignaled, MempoolBehavior mempoolBehavior, MempoolManager mempoolManager)
		{
			this.signals = signals;
			this.connectionManager = connectionManager;
			this.mempoolSignaled = mempoolSignaled;
			this.mempoolBehavior = mempoolBehavior;
			this.mempoolManager = mempoolManager;
		}

		public override void Start()
		{
			this.connectionManager.Parameters.TemplateBehaviors.Add(this.mempoolBehavior);
			this.signals.Blocks.Subscribe(this.mempoolSignaled);
		}

        public override void Stop()
        {
            if (this.mempoolManager != null)
            {
                Logs.Mempool.LogInformation("Saving Memory Pool...");

                MemPoolSaveResult result = this.mempoolManager.SavePool().GetAwaiter().GetResult();
                if (result.Succeeded)
                {
                    Logs.Mempool.LogInformation($"...Memory Pool Saved {result.TrxSaved} transactions");
                }
                else
                {
                    Logs.Mempool.LogWarning("...Memory Pool Not Saved!");
                }
            }
        }
    }

	public static class MempoolBuilderExtension
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
						services.AddSingleton<FeeRate>(MempoolValidator.MinRelayTxFee);
						services.AddSingleton<MempoolValidator>();
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
