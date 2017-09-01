using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Interfaces;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class GetInfoActionTests : BaseRPCControllerTest
    {
        [Fact]
        public void CallWithDependencies()
        {
            string dir = AssureEmptyDir("TestData/GetInfoActionTests/CallWithDependencies");
            IFullNode fullNode = this.BuildServicedNode(dir);
            FullNodeController controller = fullNode.Services.ServiceProvider.GetService<FullNodeController>();

            Assert.NotNull(fullNode.NodeService<INetworkDifficulty>(true));

            GetInfoModel info = controller.GetInfo();

            uint expectedProtocolVersion = (uint)NodeSettings.Default().ProtocolVersion;
            var expectedRelayFee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(NBitcoin.MoneyUnit.BTC);
            Assert.NotNull(info);
            Assert.Equal(0, info.blocks);
            Assert.NotEqual<uint>(0, info.version);
            Assert.Equal(expectedProtocolVersion, info.protocolversion);
            Assert.Equal(0, info.timeoffset);
            Assert.Equal(0, info.connections);
            Assert.NotNull(info.proxy);
            Assert.Equal(0, info.difficulty);
            Assert.False(info.testnet);
            Assert.Equal(expectedRelayFee, info.relayfee);
            Assert.Empty(info.errors);
        }
    }
}
