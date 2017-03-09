using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolModule : Module
	{
		public override void Configure(FullNode node, ServiceCollection serviceCollection)
		{
			// temporary types
			serviceCollection.AddSingleton(node.Chain);
			serviceCollection.AddSingleton(node.Args);
			serviceCollection.AddSingleton(node.ConnectionManager);
			serviceCollection.AddSingleton(node.CoinView);
			serviceCollection.AddSingleton(node.ConsensusLoop.Validator);
			serviceCollection.AddSingleton(node.DateTimeProvider);
			serviceCollection.AddSingleton(node.ChainBehaviorState);
			serviceCollection.AddSingleton(node.GlobalCancellation);

			serviceCollection.AddSingleton<MempoolScheduler>();
			serviceCollection.AddSingleton<TxMempool>();
			serviceCollection.AddSingleton<FeeRate>(MempoolValidator.MinRelayTxFee);
			serviceCollection.AddSingleton<MempoolValidator>();
			serviceCollection.AddSingleton<MempoolOrphans>();
			serviceCollection.AddSingleton<MempoolManager>();
			serviceCollection.AddSingleton<MempoolBehavior>();
			serviceCollection.AddSingleton<MempoolSignaled>();
		}

		public override void Start(FullNode node, IServiceProvider service)
		{
			node.ConnectionManager.Parameters.TemplateBehaviors.Add(service.GetService<MempoolBehavior>());
			node.Signals.Blocks.Subscribe(service.GetService<MempoolSignaled>());
			node.MempoolManager = service.GetService<MempoolManager>();
		}
	}
}