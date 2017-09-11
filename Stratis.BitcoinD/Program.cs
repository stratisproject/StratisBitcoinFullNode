using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

namespace Stratis.BitcoinD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (NodeSettings.PrintHelp(args, Network.Main))
            {
                RpcSettings.PrintHelp(Network.Main);
                return;
            }

            NodeSettings nodeSettings = NodeSettings.FromArguments(args);

            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddMining()
                .AddRPC()
                .Build();

            node.Run();
        }
    }
}
