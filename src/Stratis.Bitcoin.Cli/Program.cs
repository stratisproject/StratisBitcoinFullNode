using System;
using System.Linq;
using NBitcoin;
using NBitcoin.RPC;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.Bitcoin.Cli
{
    class Program
    {
        /// <summary>
        /// The expected sequence of arguments is [network-name] [rpc-command] [rpc-params].
        /// </summary>
        public static void Main(string[] args)
        {
            try
            {
                if (NodeSettings.PrintHelp(args, Network.Main))
                {
                    // TODO: add more outputs
                    // Proposal: use reflection to find all settings classes and print out help.
                    RpcSettings.PrintHelp(Network.Main);
                    return;
                }

                // hack until static flags are removed
                string blockchain = "bitcoin";
                if (args.Any(a => a.Contains("stratis")))
                {
                    blockchain = "stratis";
                    var s = Network.StratisMain;
                    var st = Network.StratisTest;
                }

                // The first argument is the network name
                var network = Network.GetNetwork(args.First());
                NodeSettings nodeSettings = NodeSettings.FromArguments(args, blockchain, network);

                var rpcSettings = new RpcSettings();
                rpcSettings.Load(nodeSettings);

                // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
                System.Net.IPEndPoint nodeEndPoint = rpcSettings.Bind.FirstOrDefault(b => b.Address.ToString() == "127.0.0.1") ?? rpcSettings.Bind[0];

                // Initilize the RPC client with the configured or passed userid, password and endpoint
                RPCClient rpc = new RPCClient($"{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"));

                // Execute the RPC command
                RPCResponse response = rpc.SendCommand(args.ElementAt(1), args.Skip(2).ToArray());

                // Return the result as a string to the console
                Console.WriteLine(response.ResultString);
            }
            catch (Exception err)
            {
                // Report any errors to the console
                Console.WriteLine(err.ToString());
            }
        }
    }
}
