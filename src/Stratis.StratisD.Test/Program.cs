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
using Stratis.StratisD.Test.Networks;

namespace Stratis.StratisD.Test
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Network network = null;
                if (args.Contains("-network=StratisPosTestNetwork"))
                    network = new StratisPosTestNetwork();

                if (args.Contains("-network=StratisPowTestNetwork"))
                    network = new StratisPowTestNetwork();

                if (network == null)
                {
                    Console.WriteLine("No network has been specified.");
                    Console.WriteLine("For a proof of stake network use: -network=StratisPosTestNetwork");
                    Console.WriteLine("For a proof of work network use: -network=StratisPowTestNetwork");
                }

                var nodeSettings = new NodeSettings(network: network, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                IFullNode node = null;
                IFullNodeBuilder builder = null;

                builder = new FullNodeBuilder()
                            .UseNodeSettings(nodeSettings)
                            .UseBlockStore();

                if (network.Consensus.IsProofOfStake)
                {
                    builder = builder
                            .UsePosConsensus()
                            .AddPowPosMining();
                }
                else
                {
                    builder = builder
                            .UsePowConsensus()
                            .AddMining();
                }

                node = builder
                            .UseMempool()
                            .UseWallet()
                            .UseApi()
                            .AddRPC()
                            .Build();

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
