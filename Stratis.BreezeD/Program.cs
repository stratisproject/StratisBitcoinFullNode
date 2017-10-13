using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Api;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.BreezeD
{
    public class Program
    {
        private const string DefaultBitcoinUri = "http://localhost:37220";
        private const string DefaultStratisUri = "http://localhost:37221";

        public static async Task Main(string[] args)
        {
            IFullNodeBuilder fullNodeBuilder = null;

            // Get the API uri. 
            var apiUri = args.GetValueOf("apiuri");
            var isTestNet = args.Contains("-testnet");
            var isStratis = args.Contains("stratis");

            NodeSettings nodeSettings;
            if (isStratis)
            {
                if (NodeSettings.PrintHelp(args, Network.StratisMain))
                    return;

                var network = isTestNet ? Network.StratisTest : Network.StratisMain;
                if (isTestNet)
                    args = args.Append("-addnode=51.141.28.47").ToArray(); // TODO: fix this temp hack 

                nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultStratisUri : apiUri);
            }
            else
            {
                nodeSettings = NodeSettings.FromArguments(args);
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultBitcoinUri : apiUri);
            }

            fullNodeBuilder = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseLightWallet()
                .UseBlockNotification()
                .UseTransactionNotification()
                .UseApi();

            IFullNode node = fullNodeBuilder.Build();

            // Start Full Node - this will also start the API.
            await node.RunAsync();
        }
    }
}
