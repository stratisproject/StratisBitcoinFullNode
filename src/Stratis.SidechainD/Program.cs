using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.SidechainGeneratorServices;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;
using System;
using System.Threading.Tasks;
namespace Stratis.SidechainD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                // To mine the premine coins, adapt and uncomment the following line.
                // args = args.Concat(new [] { "mineaddress=RaYuu3wJJ2cJkPtfb6RCbsF4aQgYrfGNqR", "mine=1" }).ToArray();
                NodeSettings nodeSettings = new NodeSettings(SidechainNetwork.SidechainTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);
                
                var node = new FullNodeBuilder()
                      .UseNodeSettings(nodeSettings)
                      .UseBlockStore()
                      .UsePowConsensus()
                      .UseMempool()
                      .UseWallet()
                      .UseApi()
                      .AddRPC()
                      .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
            Console.ReadLine();
        }
    }
}
