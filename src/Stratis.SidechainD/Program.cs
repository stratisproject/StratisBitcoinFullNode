using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;

namespace Stratis.SidechainD
{
    /// <summary>
    /// Starts a console app that includes the sidechain network parameters and that should be distributed to the sidechain users.
    /// </summary>
    class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                if (!args.Any(a => a.Contains("apiport")))
                {
                    args = args.Concat(new[] { "apiport=38225" }).ToArray();
                }

                NodeSettings nodeSettings = new NodeSettings(networksSelector: FederatedPegNetwork.NetworksSelector, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                IFullNode node = GetFederatedPegFullNode(nodeSettings);

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        private static IFullNode GetFederatedPegFullNode(NodeSettings nodeSettings)
        {
            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .UsePoAConsensus()
                .UseMempool()
                .UseWallet()
                .UseApi()
                .AddRPC()
                .Build();
            return node;
        }
    }
}
