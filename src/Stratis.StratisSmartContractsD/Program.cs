using System;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoS;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Networks;

namespace Stratis.StratisSmartContractsD
{
    class Program
    {
        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        private const int DefaultStratisApiPort = 37221;

        /// <summary>The default port used by the API when the node runs on the Stratis testnet network.</summary>
        private const int TestStratisApiPort = 38221;

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(new SmartContractsPoATest(), ProtocolVersion.ALT_PROTOCOL_VERSION, "StratisSC", args: args);
                var apiSettings = new ApiSettings(nodeSettings, (s) => {
                    s.ApiPort = nodeSettings.Network.IsTest() ? TestStratisApiPort : DefaultStratisApiPort;
                });

                Bitcoin.IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .AddRPC()
                    .AddSmartContracts(options =>
                    {
                        options.UseReflectionExecutor();
                    })
                    .UseSmartContractPoAConsensus()
                    .UseSmartContractPoAMining()
                    .UseSmartContractWallet()
                    .UseApi(apiSettings)
                    .UseMempool()
                    .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.ToString());
            }
        }
    }
}