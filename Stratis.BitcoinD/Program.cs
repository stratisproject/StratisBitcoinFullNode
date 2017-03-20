using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
			NodeSettings nodeSettings = NodeSettings.FromArguments(args);

			var node = (FullNode) new FullNodeBuilder()
				.UseNodeSettings(nodeSettings)
				.UseBlockStore()
				.UseMempool()
				.Build();

			// == shout down thread ==
			new Thread(() =>
			{
				Console.WriteLine("Press one key to stop");
				Console.ReadLine();
				node.Dispose();
			})
			{
				IsBackground = true //so the process terminate
			}.Start();

			// == mining thread ==
			if (args.Any(a => a.Contains("mine")))
			{
				new Thread(() =>
				{
					Thread.Sleep(10000); // let the node start
					while (!node.IsDisposed)
					{
						Thread.Sleep(100); // wait 1 sec
						// generate 1 block
						var res = node.Miner.GenerateBlocks(new Stratis.Bitcoin.Miner.ReserveScript(){reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey}, 1, int.MaxValue, false);
						if (res.Any())
							Console.WriteLine("mined tip at: " + node?.Chain.Tip.Height);
					}
				})
				{
					IsBackground = true //so the process terminate
				}.Start();
			}

			node.Start();
			node.WaitDisposed();
			node.Dispose();
		}
	}
}
