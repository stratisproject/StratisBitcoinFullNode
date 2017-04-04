using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Linq;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.RPC;
using Stratis.Bitcoin.Miner;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var loggerFactory = Logs.GetLoggerFactory(args);
			Logs.Configure(loggerFactory);
			
			NodeSettings nodeSettings = NodeSettings.FromArguments(args);

			if (!Checks.VerifyAccess(nodeSettings))
				return;

			var node = new FullNodeBuilder()
				.UseNodeSettings(nodeSettings)
				.UseConsensus()
				.UseBlockStore()
				.UseMempool()
				.AddMining(args.Any(a => a.Contains("mine")))
				.AddRPC()
				.Build();

			// TODO: bring the logic out of IWebHost.Run()
			node.Start();
			Console.WriteLine("Press any key to stop");
			Console.ReadLine();
			node.Dispose();
		}
	}
}
