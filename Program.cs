using System;
using System.Threading;
using Breeze.Api;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Breeze.Wallet;
using Stratis.Bitcoin.Notifications;

namespace Breeze.Daemon
{
	public class Program
    {
        public static void Main(string[] args)
        {
			// configure Full Node
			Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
			NodeSettings nodeSettings = NodeSettings.FromArguments(args);
            
            var node = (FullNode)new FullNodeBuilder()
				.UseNodeSettings(nodeSettings)
				.UseWallet()				
				.UseBlockNotification()
                .UseTransactionNotification()
				.UseApi()
				.Build();

			// start Full Node - this will also start the API
			node.Start();
			Console.WriteLine("Press any key to stop");
			Console.ReadLine();
			node.Dispose();
		}
    }
}
