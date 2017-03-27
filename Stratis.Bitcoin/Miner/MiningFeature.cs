using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using System;
using System.Linq;
using System.Threading;
using static Stratis.Bitcoin.FullNode;

namespace Stratis.Bitcoin.Miner
{
	public class MiningFeature : FullNodeFeature
	{		
		private readonly FullNode node;

		public MiningFeature(FullNode node)
		{			
			this.node = node;
		}

		public override void Start()
		{
			// == mining thread ==			
			new Thread(() =>
			{
				Thread.Sleep(10000); // let the node start
				while (!this.node.IsDisposed)
				{
					Thread.Sleep(100); // wait 1 sec
										// generate 1 block
					var res = this.node.Miner.GenerateBlocks(new Stratis.Bitcoin.Miner.ReserveScript() { reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey }, 1, int.MaxValue, false);
					if (res.Any())
						Console.WriteLine("mined tip at: " + this.node?.Chain.Tip.Height);
				}
			})
			{
				IsBackground = true //so the process terminate
			}.Start();			
		}

		public override void Stop()
		{
			Logs.Mining.LogInformation("Stopping mining process...");			
		}
	}

	public static class MiningFeatureExtension
	{
		public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder, bool doMining)
		{
			if (doMining)
			{
				fullNodeBuilder.ConfigureFeature(features =>
				{
					features
					.AddFeature<MiningFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<Mining>();
					});
				});
			}
			
			return fullNodeBuilder;
		}
	}
}
