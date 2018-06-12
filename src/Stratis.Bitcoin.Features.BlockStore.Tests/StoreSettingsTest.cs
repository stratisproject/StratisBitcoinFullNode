﻿using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{	
    public class StoreSettingsTest : TestBase	
    {	
        public StoreSettingsTest() : base(Network.Main)
        {	
        }	
	
        [Fact]	
        public void CanSpecifyStoreSettings()
        {	
            string dir = CreateTestDir(this);	
	
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dir}" });	
	
            IFullNode node1 = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .Build();	
	
            var settings1 = node1.NodeService<StoreSettings>();
	
            Assert.False(settings1.ReIndex);
            
            nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dir}", "-reindex=1" });

            IFullNode node2 = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .Build();	
	
            var settings2 = node2.NodeService<StoreSettings>();
	
            Assert.True(settings2.ReIndex);	
        }	
    }	
}