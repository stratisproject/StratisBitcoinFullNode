using System;
using System.Threading.Tasks;
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

namespace Stratis.StratisDnsD
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public class Program
    {
        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        private const int DefaultStratisApiPort = 37221;

        /// <summary>The default port used by the API when the node runs on the Stratis testnet network.</summary>
        private const int TestStratisApiPort = 38221;

        /// <summary>
        /// The async entry point for the Stratis Dns process.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>A task used to await the operation.</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector:Networks.Stratis, protocolVersion:ProtocolVersion.PROVEN_HEADER_VERSION, args:args)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                var dnsSettings = new DnsSettings(nodeSettings);

                if (string.IsNullOrWhiteSpace(dnsSettings.DnsHostName) || string.IsNullOrWhiteSpace(dnsSettings.DnsNameServer) || string.IsNullOrWhiteSpace(dnsSettings.DnsMailBox))
                    throw new ConfigurationException("When running as a DNS Seed service, the -dnshostname, -dnsnameserver and -dnsmailbox arguments must be specified on the command line.");

                var apiSettings = new ApiSettings(nodeSettings, (s) => {
                    s.ApiPort = nodeSettings.Network.IsTest() ? TestStratisApiPort : DefaultStratisApiPort;
                });

                // Run as a full node with DNS or just a DNS service?
                IFullNode node;
                if (dnsSettings.DnsFullNode)
                {
                    // Build the Dns full node.
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi(apiSettings)
                        .AddRPC()
                        .UseDns()
                        .Build();
                }
                else
                {
                    // Build the Dns node.
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UsePosConsensus()
                        .UseApi(apiSettings)
                        .AddRPC()
                        .UseDns()
                        .Build();
                }

                // Run node.
                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.ToString());
            }
        }
    }
}
