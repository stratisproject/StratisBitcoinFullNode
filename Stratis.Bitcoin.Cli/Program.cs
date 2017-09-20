using NBitcoin;
using NBitcoin.RPC;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC;
using System;
using System.Linq;

namespace Stratis.Bitcoin.Cli
{
    class Program
    {
        /// <summary>
        /// The expected sequence of arguments is [network-name] [rpc-command] [rpc-params].
        /// </summary>
        static void Main(string[] args)
        {
            if (NodeSettings.PrintHelp(args, Network.Main))
            {
                // TODO: add more outputs 
                // Proposal: use reflection to find all settings classes and print out help. 
                RpcSettings.PrintHelp(Network.Main);
                return;
            }

            // hack until static flags are removed
            if (args.Any(a => a.Contains("stratis")))
            {
                var s = Network.StratisMain;
                var st = Network.StratisTest;
            }
            var network = Network.GetNetwork(args.First());
            NodeSettings nodeSettings = NodeSettings.FromArguments(args, network.Name);

            var rpcSettings = new RpcSettings();
            rpcSettings.Load(nodeSettings);

            // TODO: make rpc port injectable to the RPCClient
            //var rpcPort = rpcSettings.RPCPort > 0 ? rpcSettings.RPCPort : network.RPCPort;

            RPCClient rpc = new RPCClient("user:pass", "http://127.0.0.1", network);
            
            var response = rpc.SendCommand(args.ElementAt(1), args.Skip(2));

            Console.WriteLine(response.ResultString);
        }
    }
}
