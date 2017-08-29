using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Tests;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public abstract class BaseRPCControllerTest : TestBase
    {
        public IFullNode BuildServicedNode(string dir)
        {
            var nodeSettings = NodeSettings.FromArguments(new string[] { $"-datadir={dir}" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .Build();

            return fullNode;
        }
    }
}
