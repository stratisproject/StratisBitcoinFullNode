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
        /// City.Chain daemon can be launched with options to specify coin and network, using the parameters -chain and -testnet. It defaults to City main network.
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
                // To avoid modifying Stratis source, we'll parse the arguments and set some hard-coded defaults for City Chain, like the ports.
                var configReader = new TextFileConfiguration(args ?? new string[] { });
                
                var networkIdentifier = "main";

                if (configReader.GetOrDefault<bool>("testnet", false))
                {
                    networkIdentifier = "testnet";
                }
                else if (configReader.GetOrDefault<bool>("regtest", false))
                {
                    networkIdentifier = "regtest";
                }

                // City Chain daemon supports multiple networks, supply the chain parameter to change it.
                // Example: -chain=bitcoin
                var chain = configReader.GetOrDefault<string>("chain", "city");

                var networkConfiguration = new NetworkConfigurations().GetNetwork(networkIdentifier, chain);

                if (networkConfiguration == null)
                {
                    throw new ArgumentException($"The supplied chain ({chain}) and network ({networkIdentifier}) parameters did not result in a valid network.");
                }

                var network = GetNetwork(networkConfiguration.Identifier, networkConfiguration.Chain);

                if (args.Contains("-generate"))
                {
                    GenerateAddressKeyPair(network);
                    return;
                }

                args = args.Append("-apiport=" + networkConfiguration.ApiPort).Append("-wsport=" + networkConfiguration.WsPort).ToArray();

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
                    //.UseBlockNotification()
                    //.UseTransactionNotification()
                    //.AddSimpleWallet()
                    .UseApi()
                    //.UseApps()
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

        public static Network GetNetwork(string network, string chain)
        {
            if (chain == "city")
            {
                if (network == "main")
                {
                    return Networks.CityMain;
                }
                else if (network == "testnet")
                {
                    return Networks.CityTest;
                }
                else if (network == "regtest")
                {
                    return Networks.CityRegTest;
                }
            }
            else if (chain == "bitcoin")
            {
                if (network == "main")
                {
                    return Networks.Main;
                }
                else if (network == "testnet")
                {
                    return Networks.TestNet;
                }
                else if (network == "regtest")
                {
                    return Networks.RegTest;
                }
            }
            else if (chain == "stratis")
            {
                if (network == "main")
                {
                    return Networks.StratisMain;
                }
                else if (network == "testnet")
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
