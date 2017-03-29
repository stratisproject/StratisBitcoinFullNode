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
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.RPC;
using Stratis.Bitcoin.Miner;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
			NodeSettings nodeSettings = NodeSettings.FromArguments(args);
			
			try 
			{
				Checks.VerifyAccess(nodeSettings);

				var node = (FullNode)new FullNodeBuilder()
					.UseNodeSettings(nodeSettings)
					.UseConsensus()
					.UseBlockStore()
					.UseMempool()
					.AddMining(args.Any(a => a.Contains("mine")))
					.AddRPC()
					.Build();

				// == shut down thread ==
				new Thread(() =>
				{
					Console.WriteLine("Press one key to stop");
					Console.ReadLine();
					node.Dispose();
				})
				{
					IsBackground = true //so the process terminates
				}.Start();

				node.Start();
				node.WaitDisposed();
				node.Dispose();
			}
			catch(UnauthorizedAccessException ex) 
			{
				Logs.Configuration.LogCritical(ex.Message);
			}
		}
	}
}
