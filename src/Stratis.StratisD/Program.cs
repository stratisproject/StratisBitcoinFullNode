using System;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.StratisD
{
    public class Program
    {
        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        public const int DefaultStratisApiPort = 37221;

        /// <summary>The default port used by the API when the node runs on the Stratis testnet network.</summary>
        public const int TestStratisApiPort = 38221;

        public static async Task Main(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                var apiSettings = new ApiSettings(nodeSettings, (s) => {
                    s.ApiPort = nodeSettings.Network.IsTest() ? TestStratisApiPort : DefaultStratisApiPort;
                });

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseColdStakingWallet()
                    .AddPowPosMining()
                    .UseApi(apiSettings)
                    .UseApps()
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
