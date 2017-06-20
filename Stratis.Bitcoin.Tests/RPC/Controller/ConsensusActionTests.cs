using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.RPC.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class ConsensusActionTests : BaseRPCControllerTest
    {
        private IFullNode fullNode;
        private ConsensusController controller;

        protected override void Initialize()
        {
            string dir = "Stratis.Bitcoin.Tests/TestData/ConsensusActionTests";
            this.fullNode = this.BuildServicedNode(dir);
            this.controller = this.fullNode.Services.ServiceProvider.GetService<ConsensusController>();
        }

        [TestMethod]
        public void CanCall_GetBestBlockHash()
        {
            uint256 result = this.controller.GetBestBlockHash();

            Assert.IsNull(result);
        }

        [TestMethod]
        public void CanCall_GetBlockHash()
        {
            uint256 result = this.controller.GetBlockHash(0);

            Assert.IsNull(result);
        }
    }
}
