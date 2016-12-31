using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));
			NodeArgs nodeArgs = NodeArgs.GetArgs(args);
			FullNode node = new FullNode(nodeArgs);
			CancellationTokenSource cts = new CancellationTokenSource();
			node.Start();
			Console.WriteLine("Press one key to stop");
			Console.ReadLine();
			node.Dispose();
		}
	}
}
