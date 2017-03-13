using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Extensions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.BitcoinD {
   public class Program {
      public static void Main(string[] args) {
         Logs.Configure(new LoggerFactory().AddConsole(LogLevel.Trace, false));
         NodeArgs nodeArgs = NodeArgs.GetArgs(args);

         //FullNode node = new FullNode(nodeArgs);

         ///actually cast to FullNode because fullnodebuilder return an interface to the node.
         ///should the node expose its internals?
         ///would be better to return FullNode instead?
         var node = (FullNode)new FullNodeBuilder()
            .UseNodeArgs(nodeArgs)
            .UseMempool()
            .Build();

         CancellationTokenSource cts = new CancellationTokenSource();
         new Thread(() => {
            Console.WriteLine("Press one key to stop");
            Console.ReadLine();
            node.Dispose();
         }) {
            IsBackground = true //so the process terminate
         }.Start();
         if (args.Any(a => a.Contains("mine"))) {
            new Thread(() => {
               Thread.Sleep(10000); // let the node start
               while (!node.IsDisposed) {
                  Thread.Sleep(1000); // wait 1 sec
                                      // generate 1 block
                  node.Miner.GenerateBlocks(new Stratis.Bitcoin.Miner.ReserveScript() {
                     reserveSfullNodecript = new NBitcoin.Key().ScriptPubKey
                  }, 1, 100000000, false);
                  Console.WriteLine("mined tip at: " + node?.Chain.Tip.Height);
               }
            }) {
               IsBackground = true //so the process terminate
            }.Start();
         }
         node.Start();
         node.WaitDisposed();
         node.Dispose();
      }
   }
}
