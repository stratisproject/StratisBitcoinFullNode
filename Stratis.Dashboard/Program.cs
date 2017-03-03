using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Dashboard {
   public class Program {
      public static void Main(string[] args) {
         Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
         NodeArgs nodeArgs = NodeArgs.GetArgs(args);
         FullNode node = new FullNode(nodeArgs);
         CancellationTokenSource cts = new CancellationTokenSource();
         new Thread(() => {
            Console.WriteLine("Press one key to stop");
            Console.ReadLine();
            node.Dispose();
         }) {
            IsBackground = true //so the process terminate
         }.Start();
         node.Start();

#if DEBUG
         var webWallet = new DashboardService(config => {
            //in debug mode, it gets files from physical path, so i set a relative path to my web content.
            //in production mode, it gets contents from embedded resource and this parameter isn't used
            var appFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            config.ContentRoot = System.IO.Path.Combine(appFolder, "..", "..", "..", "..", "Stratis.Dashboard");
            Console.WriteLine($"ContentRoot set to {config.ContentRoot}");
         });
#else
         var webWallet = new DashboardService();
#endif

         webWallet.AttachNode(node);
         webWallet.Start();

         node.WaitDisposed();
         node.Dispose();
      }
   }
}
