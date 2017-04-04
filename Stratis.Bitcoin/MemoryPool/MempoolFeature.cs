using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolFeature : FullNodeFeature 
	{
		private readonly Signals signals;
		private readonly ConnectionManager connectionManager;
		private readonly MempoolSignaled mempoolSignaled;
		private readonly MempoolBehavior mempoolBehavior;

		public MempoolFeature(ConnectionManager connectionManager, Signals signals, MempoolSignaled mempoolSignaled, MempoolBehavior mempoolBehavior)
		{
			this.signals = signals;
			this.connectionManager = connectionManager;
			this.mempoolSignaled = mempoolSignaled;
			this.mempoolBehavior = mempoolBehavior;
		}

		public override void Start()
		{
			this.connectionManager.Parameters.TemplateBehaviors.Add(this.mempoolBehavior);
			this.signals.Blocks.Subscribe(this.mempoolSignaled);
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
					});
			});

			return fullNodeBuilder;
		}
	}
}
