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
using NBitcoin;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			ILoggerFactory loggerFactory = Logs.GetLoggerFactory(args);
			Logs.Configure(loggerFactory);

			if (NodeSettings.PrintHelp(args, Network.Main))
				return;
			
			NodeSettings nodeSettings = NodeSettings.FromArguments(args);

			if (!Checks.VerifyAccess(nodeSettings))
				return;

			var node = new FullNodeBuilder()
				.UseNodeSettings(nodeSettings)
				.UseConsensus()
				.UseBlockStore()
				.UseMempool()
				.AddMining()
				.AddRPC()
				.Build();

			// TODO: bring the logic out of IWebHost.Run()
			node.Start();

			TryStartMiner(args, node);

			Console.WriteLine("Press any key to stop");
			Console.ReadLine();
			node.Dispose();
		}

		private static void TryStartMiner(string[] args, IFullNode node)
		{
			// mining can be called from either RPC or on start
			// to manage the on strat we need to get an address to the mining code
			var mine = args.FirstOrDefault(a => a.Contains("mine="));
			if (mine != null)
			{
				// get the address to mine to
				var addres = mine.Replace("mine=", string.Empty);
				var pubkey = BitcoinAddress.Create(addres, node.Network);
				node.Services.ServiceProvider.Service<PowMining>().Mine(pubkey.ScriptPubKey);
			}
		}
	}
}
