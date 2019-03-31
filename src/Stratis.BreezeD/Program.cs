using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.BreezeD
{
    public class Program
    {
        /// <summary>The default port used by the API when the node runs on the bitcoin network.</summary>
        private const int DefaultBitcoinApiPort = 37220;

        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        private const int DefaultStratisApiPort = 37221;

        /// <summary>The default port used by the API when the node runs on the bitcoin testnet network.</summary>
        private const int TestBitcoinApiPort = 38220;

        /// <summary>The default port used by the API when the node runs on the Stratis testnet network.</summary>
        private const int TestStratisApiPort = 38221;

        public static async Task Main(string[] args)
        {
            try
            {
                bool isStratis = args.Contains("stratis");

                NodeSettings nodeSettings;
                ApiSettings apiSettings;

                IFullNodeBuilder fullNodeBuilder = null;

                if (!args.Any(a => a.Contains("datadirroot")))
                    args = args.Concat(new[] { "-datadirroot=StratisBreeze" }).ToArray();

                if (isStratis)
                {
                    nodeSettings = new NodeSettings(networksSelector: Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: "Breeze", args: args)
                    {
                        MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                    };

                    apiSettings = new ApiSettings(nodeSettings, (s) =>
                    {
                        s.ApiPort = nodeSettings.Network.IsTest() ? TestStratisApiPort : DefaultStratisApiPort;
                    });

                    fullNodeBuilder = new FullNodeBuilder()
                                    .UseNodeSettings(nodeSettings)
                                    .UseApi(apiSettings)
                                    .UseBlockStore()
                                    .UsePosConsensus()
                                    .UseLightWallet()
                                    .UseBlockNotification()
                                    .UseTransactionNotification();
                }
                else
                {
                    nodeSettings = new NodeSettings(networksSelector: Networks.Bitcoin, agent: "Breeze", args: args);
                    apiSettings = new ApiSettings(nodeSettings, (s) =>
                    {
                        s.ApiPort = nodeSettings.Network.IsTest() ? TestBitcoinApiPort : DefaultBitcoinApiPort;
                    });

                    fullNodeBuilder = new FullNodeBuilder()
                                    .UseNodeSettings(nodeSettings)
                                    .UseApi(apiSettings)
                                    .UseBlockStore()
                                    .UsePowConsensus()
                                    .UseLightWallet()
                                    .UseBlockNotification()
                                    .UseTransactionNotification();
                }

                IFullNode node = fullNodeBuilder.Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a problem initializing the node: '{ex}'");
            }
        }
    }
}
