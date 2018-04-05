using System;
using System.Threading.Tasks;
using NBitcoin;
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

namespace Stratis.SidechainD
{
    class Program
    {
        static void Main(string[] args)
        {
            Program.SidechainGeneratorAsync(args).Wait();
        }

        public static async Task SidechainGeneratorAsync(string[] args)
        {
            try
            {
                var sidechainIdentifier = SidechainIdentifier.CreateFromArgs(args);
                NodeSettings nodeSettings = new NodeSettings(SidechainNetwork.SidechainRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddSidechainGeneratorServices()
                    .UseApi()
                    .AddRPC()
                    .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}
