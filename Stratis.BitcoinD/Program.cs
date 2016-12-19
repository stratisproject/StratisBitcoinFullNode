using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin.FullNode.Configuration;
using Stratis.Bitcoin.FullNode.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.BitcoinD
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));
			var dataDir = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			NodeArgs nodeArgs = NodeArgs.GetArgs(dataDir, args);
		}
	}
}
