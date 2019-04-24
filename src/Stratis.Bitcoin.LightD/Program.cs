using System;
using System.Threading.Tasks;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Utilities;
using Network = Stratis.Bitcoin.Networks.Networks;

namespace Stratis.Bitcoin.LightD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Network.Bitcoin, args: args);

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseLightPowConsensus()
                    .AddRPC()
                    .UseTransactionNotification()
                    .UseBlockNotification()
                    .UseLightWallet()
                    .UseApi()
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