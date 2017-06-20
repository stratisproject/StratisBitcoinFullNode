using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.IntegrationTests
{
	public static class AssemblyInitialize
	{
        [AssemblyInitialize]
		public static void Initialize()
		{
			Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));			
		}		
	}
}
