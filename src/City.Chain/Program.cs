namespace City.Chain
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Blockcore.Features.WalletNotify;
    using City.Features.BlockExplorer;
    using City.Networks;
    using NBitcoin;
    using NBitcoin.Protocol;
    using Stratis.Bitcoin;
    using Stratis.Bitcoin.Builder;
    using Stratis.Bitcoin.Configuration;
    using Stratis.Bitcoin.Features.Api;
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
        /// dotnet city.chain.dll -coin=bitcoin -network=regtest
        /// dotnet city.chain.dll -coin=city -network=test
        /// </example>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            try
            {
                var configReader = new TextFileConfiguration(args ?? new string[] { });

                // City Chain daemon supports multiple networks, supply the chain parameter to change it.
                // Example: -chain=bitcoin
                var chain = configReader.GetOrDefault<string>("chain", "city");

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
					.Append("-datadirroot=CityChain") // DataDirRoot can be supplied to specify where to locate files, make sure it is always set to CityChain.
					.Append("-apiport=" + apiPort)
                    .Append("-wsport=" + networkConfiguration.WsPort).ToArray();

                var nodeSettings = new NodeSettings(networksSelector: GetNetwork(chain), protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args, agent: "CityChain")
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                var dnsSettings = new DnsSettings(nodeSettings);

                // Create or read the node info.
                var nodeInfoManager = new NodeInfoManager(nodeSettings);

                // Indicates if we should clear the blockchain database. When this is performed, the daemon will exit immediately.
                if (configReader.GetOrDefault<bool>("reset", false))
                {
                    Console.WriteLine("Reset option was supplied, blockchain database is being reset.");
                    nodeInfoManager.ClearBlockchainDatabase();
                    Console.WriteLine("Blockchain database was successfully reset. Exiting.");
                    return;
                }

                var nodeInfo = nodeInfoManager.CreateOrReadNodeInfo();

                // Perform any migrations if there is any waiting.
                nodeInfoManager.PerformMigration(nodeInfo);

                IFullNode node;

                if (dnsSettings.DnsFullNode)
                {
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UseBlockExplorer()
                        .UsePosConsensus()
                        .UseMempool()
						.UseColdStakingWallet()
						//.UseWallet()
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
                        .UseColdStakingWallet()
                        //.UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .UseWalletNotify()
                        .AddRPC()
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
    }
}
