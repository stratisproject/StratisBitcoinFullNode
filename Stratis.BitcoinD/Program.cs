using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
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
			NodeArgs nodeArgs = NodeArgs.GetArgs(args);
			Console.WriteLine("Press one key to stop");
			Console.ReadLine();		
		}
	}
}
