using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools
{
    public class PoANodeRunner : NodeRunner
    {
        public PoANodeRunner(string dataDir, PoANetwork network)
            : base(dataDir)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePoAConsensus()
                .UseMempool()
                .UseWallet()
                .UseApi()
                .AddRPC()
                .Build();
        }
    }
}
