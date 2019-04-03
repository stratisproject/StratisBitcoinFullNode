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
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    /// <summary>
    /// To emulate the behaviour of a main chain node in FederationGatewayD.
    /// </summary>
    public class MainChainFederationNodeRunner : NodeRunner
    {
        private Network counterChainNetwork;

        public MainChainFederationNodeRunner(string dataDir, string agent, Network network, Network counterChainNetwork)
            : base(dataDir, agent)
        {
            this.Network = network;
            this.counterChainNetwork = counterChainNetwork;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, ProtocolVersion.PROVEN_HEADER_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .AddFederationGateway(new FederatedPegOptions(this.counterChainNetwork))
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .UseMempool()
                .AddRPC()
                .UsePosConsensus()
                .UseWallet()
                .AddPowPosMining()
                .MockIBD()
                .Build();
        }
    }
}
