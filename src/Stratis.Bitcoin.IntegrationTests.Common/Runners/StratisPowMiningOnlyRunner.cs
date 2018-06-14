using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisProofOfWorkMiningNode : NodeRunner
    {
        public StratisProofOfWorkMiningNode(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UseBlockStore()
                            .UsePosConsensus()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddRPC()
                            .MockIBD()
                            .SubstituteDateTimeProviderFor<MiningFeature>()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}