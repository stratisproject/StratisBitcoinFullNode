using System;
using System.Threading.Tasks;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.BitcoinD
{
    public class Program
    {
        /// <summary>The default port used by the API when the node runs on the bitcoin network.</summary>
        private const int DefaultBitcoinApiPort = 37220;

        /// <summary>The default port used by the API when the node runs on the bitcoin testnet network.</summary>
        private const int TestBitcoinApiPort = 38220;

        public static async Task Main(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Networks.Bitcoin, args: args);
                var apiSettings = new ApiSettings(nodeSettings, (s) => {
                    s.ApiPort = nodeSettings.Network.IsTest() ? TestBitcoinApiPort : DefaultBitcoinApiPort;
                });

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePowConsensus()
                    .UseMempool()
                    .AddMining()
                    .AddRPC()
                    .UseWallet()
                    .UseApi(apiSettings)
                    .UseApps()
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
