using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
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
using Stratis.FederatedPeg.Features.MainchainGeneratorServices;

namespace Stratis.StratisD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            SidechainGeneratorAsync(args).Wait();
        }

        public static async Task SidechainGeneratorAsync(string[] args)
        {
            try
            {
                Network network = Network.StratisRegTest;
                NodeSettings nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args, loadConfiguration: false);

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddMainchainGeneratorServices()
                    .UseApi()
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
    }
}
