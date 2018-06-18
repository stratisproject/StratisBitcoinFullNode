using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisPosApiRunner : NodeRunner
    {
        public StratisPosApiRunner(string dataDir)
            : base(dataDir)
        {
            this.Network = Network.StratisRegTest;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UseBlockStore()
                            .UsePosConsensus()
                            .UseMempool()
                            .AddPowPosMining()
                            .UseWallet()
                            .UseApi()
                            .AddRPC()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}