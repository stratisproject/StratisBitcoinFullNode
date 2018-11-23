using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.Sidechains.Networks;

namespace Stratis.FederationGatewayD
{
    class Program
    {
        private const string MainchainArgument = "-mainchain";
        private const string SidechainArgument = "-sidechain";

        static void Main(string[] args)
        {
            RunFederationGatewayAsync(args).Wait();
        }

        public static async Task RunFederationGatewayAsync(string[] args)
        {
            try
            {
                var isMainchainNode = args.FirstOrDefault(a => a.ToLower() == MainchainArgument) != null;
                var isSidechainNode = args.FirstOrDefault(a => a.ToLower() == SidechainArgument) != null;

                if (isSidechainNode == isMainchainNode)
                {
                    throw new ArgumentException($"Gateway node needs to be started specifying either a {SidechainArgument} or a {MainchainArgument} argument");
                }

                var nodeSettings = new NodeSettings(networksSelector: isMainchainNode ? Networks.Stratis : FederatedPegNetwork.NetworksSelector, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);
                Network network = nodeSettings.Network;

                IFullNode node = isMainchainNode
                    ? new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .UseTransactionNotification()
                        .UseBlockNotification()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseApi()
                        .AddRPC()
                        .Build()
                    : GetFederatedPegFullNode(nodeSettings);

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
                .UseTransactionNotification()
                .UseBlockNotification()
                .AddFederationGateway()
                .UseApi()
                //.UseApps()
                .AddRPC()
                .Build();
            return node;
        }
    }
}
