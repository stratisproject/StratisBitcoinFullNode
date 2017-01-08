using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests
{
	class Program
	{
		public static void Main(string[] args)
		{
			Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));
			new Class1().ValidSomeBlocksOnMainnet();
		}

		static Random _Rand = new Random();
	}
}
