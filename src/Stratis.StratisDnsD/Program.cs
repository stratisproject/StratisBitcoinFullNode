using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
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
using Stratis.Bitcoin.Utilities;

namespace Stratis.StratisDnsD
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The entry point for the Stratis Dns process.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        /// <summary>
        /// The async entry point for the Stratis Dns process.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>A task used to await the operation.</returns>
        public static async Task MainAsync(string[] args)
        {
            try
            {
                Network network = args.Contains("-testnet") ? Network.StratisTest : Network.StratisMain;
                NodeSettings nodeSettings = new NodeSettings("stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION).LoadArguments(args);
                DnsSettings dnsSettings = new DnsSettings(nodeSettings);

                // Verify that the DNS host, nameserver and mailbox arguments are set.
                if (string.IsNullOrWhiteSpace(dnsSettings.DnsHostName) || string.IsNullOrWhiteSpace(dnsSettings.DnsNameServer) || string.IsNullOrWhiteSpace(dnsSettings.DnsMailBox))
                {
                    throw new ArgumentException("When running as a DNS Seed service, the -dnshostname, -dnsnameserver and -dnsmailbox arguments must be specified on the command line.");
                }

                // Run as a full node with DNS or just a DNS service?
                if (dnsSettings.DnsFullNode)
                {
                    // Build the Dns full node.
                    IFullNode node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseStratisConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .AddRPC()
                        .UseDns()
                        .Build();

                    // Run node.
                    await node.RunAsync();
                }
                else
                {
                    // Build the Dns node.
                    IFullNode node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseStratisConsensus()
                        .UseApi()
                        .AddRPC()
                        .UseDns()
                        .Build();

                    // Run node.
                    await node.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}
