using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;
using System;
using System.Threading.Tasks;

namespace Stratis.BitcoinD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            NodeSettings nodeSettings;
            try
            {
                nodeSettings = NodeSettings.FromArguments(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem in the arguments passed. Details: '{0}'", ex.Message);
                return;
            }
            
            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddMining()
                .AddRPC()
                .Build();

            await node.RunAsync();
        }
    }
}
