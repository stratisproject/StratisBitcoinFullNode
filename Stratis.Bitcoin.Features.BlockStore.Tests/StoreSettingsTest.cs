using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class StoreSettingsTest : TestBase
    {
        [Fact]
        public void CanSpecifyStoreSettings()
        {
            var dir = AssureEmptyDir("TestData/StoreSettingsTest/CanSpecifyStoreSettings");

            NodeSettings nodeSettings = NodeSettings.FromArguments(new string[] { $"-datadir={dir}" });

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