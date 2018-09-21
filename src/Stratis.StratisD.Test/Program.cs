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
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.StratisD.Test
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Network network = null;
                if (args.Contains("-network=StratisRegPosTest"))
                    network = new Networks.StratisRegPosTest();

                if (args.Contains("-network=StratisRegPowTest"))
                    network = new Networks.StratisRegPowTest();

                if (network == null)
                {
                    Console.WriteLine("No network has been specified.");
                    Console.WriteLine("For proof of stake network use: -network=StratisRegPosTest");
                    Console.WriteLine("For proof of work network use: -network=StratisRegPowTest");
                }

                var nodeSettings = new NodeSettings(network: network, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                IFullNode node = null;
                if (network.Consensus.IsProofOfStake)
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .AddRPC()
                        .Build();
                else
                {
                    node = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePowConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddMining()
                        .UseApi()
                        .AddRPC()
                        .Build();
                }

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
