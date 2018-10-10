using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class StoreSettingsTest : TestBase	
    {	
        public StoreSettingsTest() : base(KnownNetworks.Main)
        {	
        }	
	
        [Fact]	
        public void CanSpecifyStoreSettings()
        {
            string dir = CreateTestDir(this);

            var nodeSettings = new NodeSettings(this.Network, args: new string[] { $"-datadir={dir}" });

            IFullNode node1 = FullNodeSetup(nodeSettings);

            var settings1 = node1.NodeService<StoreSettings>();

            Assert.False(settings1.ReIndex);

            nodeSettings = new NodeSettings(this.Network, args: new string[] { $"-datadir={dir}", "-reindex=1" });

            IFullNode node2 = FullNodeSetup(nodeSettings);

            var settings2 = node2.NodeService<StoreSettings>();

            Assert.True(settings2.ReIndex);
        }

        private static IFullNode FullNodeSetup(NodeSettings nodeSettings)
        {
            return new FullNodeBuilder()
                            .UseNodeSettings(nodeSettings)
                            .UseBlockStore()
                            .UsePowConsensus()
                            .Build();
        }
    }	
}