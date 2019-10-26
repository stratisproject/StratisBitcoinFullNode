using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class RPCSettingsTest : TestBase
    {
        public RPCSettingsTest() : base(KnownNetworks.Main)
        {
        }

        [Fact]
        public void CanSpecifyRPCSettings()
        {
            string dir = CreateTestDir(this);

            var nodeSettings = new NodeSettings(this.Network, args: new string[] { $"-datadir={dir}", "-rpcuser=abc", "-rpcpassword=def", "-rpcport=91", "-server=1" });

            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UsePowConsensus()
                .AddRPC()
                .Build();

            var settings = node.NodeService<RpcSettings>();

            Assert.Equal("abc", settings.RpcUser);
            Assert.Equal("def", settings.RpcPassword);
            Assert.Equal(91, settings.RPCPort);
        }
    }
}