using System;
using System.Linq;
using System.Threading;
using Breeze.Api;
using Breeze.TumbleBit;
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
            
            var fullNodeBuilder = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseWallet()
                .UseBlockNotification()
                .UseTransactionNotification()
                .UseApi();

            // add the tumbler's settings
            var tumblerAddress = args.SingleOrDefault(arg => arg.StartsWith("-tumbler-uri="));
            if (!string.IsNullOrEmpty(tumblerAddress))
            {
                tumblerAddress = tumblerAddress.Replace("-tumbler-uri=", string.Empty);
                fullNodeBuilder.UseTumbleBit(new Uri(tumblerAddress));
            }
            
            var node = (FullNode)fullNodeBuilder.Build();
            
            // start Full Node - this will also start the API
            node.Start();
            Console.WriteLine("Press any key to stop");
            Console.ReadLine();
            node.Dispose();
        }
    }
}
