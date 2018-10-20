namespace City.Chain
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using City.Features.BlockExplorer;
    using City.Networks;
    using NBitcoin;
    using NBitcoin.Networks;
    using NBitcoin.Protocol;
    using Stratis.Bitcoin;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Features.Api;
    using Stratis.Bitcoin.Features.BlockStore;
    using Stratis.Bitcoin.Features.Consensus;
    using Stratis.Bitcoin.Features.Dns;
    using Stratis.Bitcoin.Features.MemoryPool;
    using Stratis.Bitcoin.Features.Miner;
    using Stratis.Bitcoin.Features.RPC;
    using Stratis.Bitcoin.Features.Wallet;
    using Stratis.Bitcoin.Networks;
    using Stratis.Bitcoin.Utilities;

    public class Program
    {
        private static void GenerateAddressKeyPair(Network network)
        {
            Key privateKey;
            privateKey = new Key();
            var addressString = privateKey.PubKey.GetAddress(network).ToString();
            var privateKeyString = privateKey.GetWif(network).ToWif().ToString();

            Console.WriteLine(addressString);
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

                // TODO: Perform full test validation of Stratis and Bitcoin before adding support for it.
                chain = "city";

                NetworkConfiguration networkConfiguration = new NetworkConfigurations().GetNetwork(networkIdentifier, chain);

                if (networkConfiguration == null)
                {
                    throw new ArgumentException($"The supplied chain ({chain}) and network ({networkIdentifier}) parameters did not result in a valid network.");
                }

                Network network = GetNetwork(networkConfiguration.Identifier, networkConfiguration.Chain);

                // Register the network found.
                NetworkRegistration.Register(network);

                if (args.Contains("-generate"))
                {
                    GenerateAddressKeyPair(network);
                    return;
                }

                var apiPort = configReader.GetOrDefault<string>("apiport", networkConfiguration.ApiPort.ToString());

                args = args
                    .Append("-apiport=" + apiPort)
                    .Append("-txindex=1") // Required for History (Block) explorer.
                    .Append("-wsport=" + networkConfiguration.WsPort).ToArray();

                var nodeSettings = new NodeSettings(
                    args: args,
                    protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION,
                    network: network,
                    agent: "CityChain");

                // Write the schema version, if not already exists.
                var infoPath = System.IO.Path.Combine(nodeSettings.DataDir, "city.info");

                if (!System.IO.File.Exists(infoPath))
                {
                    // For clients earlier than this version, the database already existed so we'll
                    // write that it is currently version 100.
                    var infoBuilder = new System.Text.StringBuilder();

                    // If the chain exists from before, but we did not have .info file, the database is old version.
                    if (System.IO.Directory.Exists(Path.Combine(nodeSettings.DataDir, "chain")))
                    {
                        infoBuilder.AppendLine("dbversion=100");
                    }
                    else
                    {
                        infoBuilder.AppendLine("dbversion=110");
                    }

                    File.WriteAllText(infoPath, infoBuilder.ToString());
                }
                else
                {
                    var fileConfig = new TextFileConfiguration(File.ReadAllText(infoPath));
                    var dbversion = fileConfig.GetOrDefault<int>("dbversion", 110);
                }

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseBlockExplorer()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    //.UseBlockNotification()
                    //.UseTransactionNotification()
                    //.AddSimpleWallet()
                    .UseApi()
                    //.UseApps()
                    //.UseDns()
                    .AddRPC()
                    .Build();

                if (node != null)
                {
                    await node.RunAsync();
                }
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
                    return new CityMain();
                }
                else if (network == "testnet")
                {
                    return new CityTest();
                }
                else if (network == "regtest")
                {
                    return new CityRegTest();
                }
            }
            else if (chain == "bitcoin")
            {
                if (network == "main")
                {
                    return new BitcoinMain();
                }
                else if (network == "testnet")
                {
                    return new BitcoinTest();
                }
                else if (network == "regtest")
                {
                    return new BitcoinRegTest();
                }
            }
            else if (chain == "stratis")
            {
                if (network == "main")
                {
                    return new StratisMain();
                }
                else if (network == "testnet")
                {
                    return new StratisTest();
                }
                else if (network == "regtest")
                {
                    return new StratisRegTest();
                }
            }

            return null;
        }
    }
}
