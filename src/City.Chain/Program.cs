using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using City.Chain.Features.SimpleWallet;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace City.Chain
{
    public class Program
    {
        private static void GenerateAddressKeyPair(Network network)
        {
            Key privateKey;
            privateKey = new Key();
            var AddressString = privateKey.PubKey.GetAddress(network).ToString();
            var privateKeyString = privateKey.GetWif(network).ToWif().ToString();

            Console.WriteLine(AddressString);
            Console.WriteLine(privateKeyString);
        }

        /// <summary>
        /// City.Chain daemon can be launched with options to specify coin and network, using the parameters -coin and -network. It defaults to City main network.
        /// </summary>
        /// <example>
        /// dotnet city.chain.dll -coin bitcoin -network regtest
        /// dotnet city.chain.dll -coin city -network test
        /// </example>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            try
            {
                var coinIndex = Array.IndexOf(args, "-coin");
                var coinValue = (coinIndex > -1) ? args[coinIndex + 1] : "city";

                var networkIndex = Array.IndexOf(args, "-network");
                var networkValue = (networkIndex > -1) ? args[networkIndex + 1] : "";

                var network = GetNetwork(coinValue, networkValue);

                if (network == null)
                {
                    throw new ArgumentNullException($"The supplied coin ({coinValue}) and network ({networkValue}) parameters did not result in a valid network.");
                }

                if (args.Contains("-generate"))
                {
                    GenerateAddressKeyPair(network);
                    return;
                }

                var nodeSettings = new NodeSettings(
                    args: args,
                    protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION,
                    network: network,
                    agent: "CityChain");

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseBlockNotification()
                    .UseTransactionNotification()
                    .AddSimpleWallet()
                    .UseApi()
                    .UseApps()
                    .AddRPC()
                    .Build();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        public static Network GetNetwork(string coin, string network)
        {
            if (coin == "city")
            {
                if (network == "")
                {
                    return Networks.CityMain;
                }
                else if (network == "test")
                {
                    return Networks.CityTest;
                }
                else if (network == "regtest")
                {
                    return Networks.CityRegTest;
                }
            }
            else if (coin == "bitcoin")
            {
                if (network == "")
                {
                    return Networks.Main;
                }
                else if (network == "test")
                {
                    return Networks.TestNet;
                }
                else if (network == "regtest")
                {
                    return Networks.RegTest;
                }
            }
            else if (coin == "stratis")
            {
                if (network == "")
                {
                    return Networks.StratisMain;
                }
                else if (network == "test")
                {
                    return Networks.StratisTest;
                }
                else if (network == "regtest")
                {
                    return Networks.StratisRegTest;
                }
            }

            return null;
        }
    }
}
