using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolFeature : FullNodeFeature 
	{
		private readonly FullNode fullNode;
		private readonly MempoolManager manager;

		public MempoolFeature(FullNode fullNode, MempoolManager manager)
		{
			this.fullNode = fullNode;
			this.manager = manager;
		}

		public override void Start()
		{
			// TODO: move service resolver types to the constructor
			this.fullNode.ConnectionManager.Parameters.TemplateBehaviors.Add(this.fullNode.Services.ServiceProvider.GetService<MempoolBehavior>());
			this.fullNode.Signals.Blocks.Subscribe(this.fullNode.Services.ServiceProvider.GetService<MempoolSignaled>());

			this.fullNode.MempoolManager = this.manager;
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
