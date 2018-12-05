namespace City.Chain
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using City.Features.BlockExplorer;
    using City.Networks;
    using NBitcoin;
    using NBitcoin.Protocol;
    using Stratis.Bitcoin;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Features.Api;
    using Stratis.Bitcoin.Features.Apps;
    using Stratis.Bitcoin.Features.BlockStore;
    using Stratis.Bitcoin.Features.ColdStaking;
    using Stratis.Bitcoin.Features.Consensus;
    using Stratis.Bitcoin.Features.Dns;
    using Stratis.Bitcoin.Features.MemoryPool;
    using Stratis.Bitcoin.Features.Miner;
    using Stratis.Bitcoin.Features.RPC;
    using Stratis.Bitcoin.Features.Wallet;
    using Stratis.Bitcoin.Utilities;

    public class Program
    {
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

                // City Chain daemon supports multiple networks, supply the chain parameter to change it.
                // Example: -chain=bitcoin
                var chain = configReader.GetOrDefault<string>("chain", "city");

                // TODO: Perform full test validation of Stratis and Bitcoin before adding support for it.
                //chain = "city";

                var networkIdentifier = "main";

                if (configReader.GetOrDefault<bool>("testnet", false))
                {
                    networkIdentifier = "testnet";
                }
                else if (configReader.GetOrDefault<bool>("regtest", false))
                {
                    networkIdentifier = "regtest";
                }

                NetworkConfiguration networkConfiguration = new NetworkConfigurations().GetNetwork(networkIdentifier, chain);

                if (networkConfiguration == null)
                {
                    throw new ArgumentException($"The supplied chain ({chain}) and network ({networkIdentifier}) parameters did not result in a valid network.");
                }

                var apiPort = configReader.GetOrDefault<string>("apiport", networkConfiguration.ApiPort.ToString());

                args = args
                    .Append("-apiport=" + apiPort)
                    .Append("-txindex=1") // Required for History (Block) explorer.
                    .Append("-wsport=" + networkConfiguration.WsPort).ToArray();

                var nodeSettings = new NodeSettings(networksSelector: GetNetwork(chain), protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args, agent: "CityChain")
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                var dnsSettings = new DnsSettings(nodeSettings);

                WriteDatabaseSchemaInfo(nodeSettings);

                IFullNode node;

                if (dnsSettings.DnsFullNode)
                {
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UseBlockExplorer()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .UseDns()
                        .AddRPC()
                        .Build();
                }
                else
                {
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UseBlockExplorer()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .UseColdStakingWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .UseApps()
                        .AddRPC()
                        //.AddSimpleWallet()
                        //.UseBlockNotification()
                        //.UseTransactionNotification()
                        .Build();
                }

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

        public static NetworksSelector GetNetwork(string chain)
        {
            if (chain == "city")
            {
                return CityNetworks.City;
            }
            else if (chain == "bitcoin")
            {
                return Stratis.Bitcoin.Networks.Networks.Bitcoin;
            }
            else if (chain == "stratis")
            {
                return Stratis.Bitcoin.Networks.Networks.Stratis;
            }

            return null;
        }

        private static void WriteDatabaseSchemaInfo(NodeSettings nodeSettings)
        {
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
        }
    }
}
