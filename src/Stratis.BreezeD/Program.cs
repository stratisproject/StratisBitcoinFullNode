using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Networks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.BreezeD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                // Get the API uri.
                bool isTestNet = args.Contains("-testnet");
                bool isStratis = args.Contains("stratis");

                string agent = "Breeze";

                NodeSettings nodeSettings;

                if (isStratis)
                {
                    if (isTestNet)
                        args = args.Append("-addnode=51.141.28.47").ToArray(); // TODO: fix this temp hack

                    nodeSettings = new NodeSettings(networksSelector:Networks.Stratis, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, agent: agent, args:args);
                }
                else
                {
                    nodeSettings = new NodeSettings(networksSelector:Networks.Bitcoin, agent: agent, args: args);
                }

                IFullNodeBuilder fullNodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseLightWallet()
                    .UseBlockNotification()
                    .UseTransactionNotification()
                    .UseApi();

                IFullNode node = fullNodeBuilder.Build();

                // Start Full Node - this will also start the API.
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
