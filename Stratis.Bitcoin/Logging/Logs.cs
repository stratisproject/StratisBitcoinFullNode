using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Logging
{
	public class Logs
	{
		static Logs()
		{
			Configure(new FuncLoggerFactory(n => new NullLogger()));
		}
		public static void Configure(ILoggerFactory factory)
		{
			Configuration = factory.CreateLogger("Configuration");
		}
		public static ILogger Configuration
		{
			get; set;
		}
	}
}
