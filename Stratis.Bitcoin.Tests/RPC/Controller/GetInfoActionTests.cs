using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC.Controllers;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class GetInfoActionTests : BaseRPCControllerTest
    {
        [TestMethod]
        public void CallWithDependencies()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/GetInfoActionTests/CallWithDependencies");
            IFullNode fullNode = this.BuildServicedNode(dir);
            FullNodeController controller = fullNode.Services.ServiceProvider.GetService<FullNodeController>();

            GetInfoModel info = controller.GetInfo();

            uint expectedProtocolVersion = (uint)NodeSettings.Default().ProtocolVersion;
            var expectedRelayFee = MempoolValidator.MinRelayTxFee.FeePerK.ToUnit(NBitcoin.MoneyUnit.BTC);
            Assert.IsNotNull(info);
            Assert.AreEqual(0, info.blocks);
            Assert.AreNotEqual<uint>(0, info.version);
            Assert.AreEqual(expectedProtocolVersion, info.protocolversion);
            Assert.AreEqual(0, info.timeoffset);
            Assert.AreEqual(0, info.connections);
            Assert.IsNotNull(info.proxy);
            Assert.AreEqual(0, info.difficulty);
            Assert.IsFalse(info.testnet);
            Assert.AreEqual(expectedRelayFee, info.relayfee);
            Assert.AreEqual(string.Empty, info.errors);
        }

    }
}
