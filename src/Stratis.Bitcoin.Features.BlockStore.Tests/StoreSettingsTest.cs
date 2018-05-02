using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class StoreSettingsTest : TestBase
    {
        public StoreSettingsTest()
        {
            // Ensure that these flags match the Network and NetworkOptions being used
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        [Fact]
        public void CanSpecifyStoreSettings()
        {
            string dir = CreateTestDir(this);

            NodeSettings nodeSettings = new NodeSettings(args:new string[] { $"-datadir={dir}" }, loadConfiguration:false);

            var node1 = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .Build();

            var settings1 = node1.NodeService<StoreSettings>();

            settings1.Load(nodeSettings);

            Assert.False(settings1.ReIndex);

            var node2 = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore(x => x.ReIndex = true)
                .Build();

            var settings2 = node2.NodeService<StoreSettings>();

            settings2.Load(nodeSettings);

            Assert.True(settings2.ReIndex);
        }
    }
}
