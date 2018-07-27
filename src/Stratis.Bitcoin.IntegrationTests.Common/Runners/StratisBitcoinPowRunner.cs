using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(string dataDir)
            : base(dataDir)
        {
            this.Network = KnownNetworks.RegTest;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePowConsensus()
                .UseMempool()
                .AddMining()
                .UseWallet()
                .AddRPC()
                .MockIBD()
                .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}