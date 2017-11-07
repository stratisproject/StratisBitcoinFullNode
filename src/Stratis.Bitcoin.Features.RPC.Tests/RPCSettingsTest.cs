using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCSettingsTest : TestBase
    {
        [Fact]
        public void CanSpecifyRPCSettings()
        {
            var dir = AssureEmptyDir("TestData/StoreSettingsTest/CanSpecifyRPCSettings");

            NodeSettings nodeSettings = NodeSettings.FromArguments(new string[] { $"-datadir={dir}" });

            var node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .AddRPC(x => {
                    x.RpcUser = "abc";
                    x.RpcPassword = "def";
                    x.RPCPort = 91;
                    })
                .Build();

            var settings = node.NodeService<RpcSettings>();

            settings.Load(nodeSettings);

            Assert.Equal("abc", settings.RpcUser);
            Assert.Equal("def", settings.RpcPassword);
            Assert.Equal(91, settings.RPCPort);
        }
    }
}