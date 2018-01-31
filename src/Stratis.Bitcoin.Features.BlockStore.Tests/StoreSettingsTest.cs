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
            string dir = CreateTestDir(this);

            var node1 = new FullNodeBuilder()
                .UseNodeSettings(new NodeSettings(args:new string[] { $"-datadir={dir}.1" }))
                .UseBlockStore()
                .Build();
            
            var settings1 = node1.NodeService<StoreSettings>();
       
            var node2 = new FullNodeBuilder()
                .UseNodeSettings(new NodeSettings(args:new string[] { $"-datadir={dir}.2" }))
                .UseBlockStore(x => x.ReIndex = true)
                .Build();
            
            var settings2 = node2.NodeService<StoreSettings>();

            Assert.False(settings1.ReIndex);
            Assert.True(settings2.ReIndex);            
        }
    }
}
