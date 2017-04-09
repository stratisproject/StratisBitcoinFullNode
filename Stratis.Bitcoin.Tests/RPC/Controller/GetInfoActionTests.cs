using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC.Controllers;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class GetInfoActionTests : TestBase
    {
        [Fact]
        public void CallWithoutDependencies()
        {
            var controller = new FullNodeController();

            GetInfoModel info = controller.GetInfo();

            Assert.NotNull(info);
            Assert.NotNull(info.version);
            Assert.NotNull(info.protocolversion);
            Assert.NotNull(info.blocks);
            Assert.NotNull(info.timeoffset);
            Assert.Null(info.connections);
            Assert.NotNull(info.proxy);
            Assert.NotNull(info.difficulty);
            Assert.NotNull(info.testnet);
            Assert.NotNull(info.relayfee);
            Assert.NotNull(info.errors);
            Assert.Null(info.walletversion);
            Assert.Null(info.balance);
            Assert.Null(info.keypoololdest);
            Assert.Null(info.keypoolsize);
            Assert.Null(info.unlocked_until);
            Assert.Null(info.paytxfee);

        }

        [Fact]
        public void CallWithDependencies()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/GetInfoActionTests/CallWithDependencies");
            IFullNode fullNode = RPCControllerTest.BuildServicedNode(dir);
            FullNodeController controller = fullNode.Services.ServiceProvider.GetService<FullNodeController>();

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
