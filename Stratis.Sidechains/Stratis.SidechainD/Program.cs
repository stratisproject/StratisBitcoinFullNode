using System;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SidechainD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                var sidechainIdentifier = SidechainIdentifier.Create("enigma");
                NodeSettings nodeSettings = new NodeSettings(sidechainIdentifier.Name, Network.SidechainTestNet, ProtocolVersion.ALT_PROTOCOL_VERSION).LoadArguments(args);

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}