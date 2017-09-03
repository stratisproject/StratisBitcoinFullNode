using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
            if (NodeSettings.PrintHelp(args, Network.Main))
            {
                RpcSettings.PrintHelp(Network.Main);
                return;
            }

            NodeSettings nodeSettings = NodeSettings.FromArguments(args);

			var node = new FullNodeBuilder()
				.UseNodeSettings(nodeSettings)
				.UseConsensus()
				.UseBlockStore()
				.UseMempool()
				.AddMining()
				.AddRPC()
				.Build();

			// start the miner (this is temporary a miner should be started using RPC.
			Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith((t) => { TryStartMiner(args, node); });

			node.Run();
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
				node.Services.ServiceProvider.GetService<PowMining>().Mine(pubkey.ScriptPubKey);
			}
		}
	}
}
