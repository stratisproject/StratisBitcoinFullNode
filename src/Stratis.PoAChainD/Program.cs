using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.PoAChainD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Network network = new PoANetwork();
                var nodeSettings = new NodeSettings(args: args, network: network);

                bool keyGenerationRequired = nodeSettings.ConfigReader.GetOrDefault("generateKeyPair", false);
                if (keyGenerationRequired)
                {
                    GenerateFederationKey(nodeSettings.DataFolder);
                    return;
                }

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePoAConsensus()
                    .UseMempool()
                    .UseWallet()
                    .UseApi()
                    .UseApps()
                    .AddRPC()
                    .Build();

                if (node != null)
                {
                    await node.RunAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem running the node. Details: '{0}'", ex.ToString());
            }
        }

        private static void GenerateFederationKey(DataFolder dataFolder)
        {
            var tool = new KeyTool(dataFolder);
            Key key = tool.GeneratePrivateKey();

            string savePath = tool.GetPrivateKeySavePath();
            tool.SavePrivateKey(key);

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Federation key pair was generated and saved to {savePath}.");
            stringBuilder.AppendLine("Make sure to back it up!");
            stringBuilder.AppendLine($"Your public key is {key.PubKey}.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Press eny key to exit...");

            Console.WriteLine(stringBuilder.ToString());

            Console.ReadKey();
        }
    }
}
