using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Controllers
{
    public class SmartContractsControllerTest
    {
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Network network;
        private readonly Mock<IStateRepositoryRoot> stateRoot;

        public SmartContractsControllerTest()
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = new SmartContractsRegTest();
            this.stateRoot = new Mock<IStateRepositoryRoot>();
        }

        [Fact]
        public void Balance_IsIn_Strat()
        {
            this.stateRoot
                .Setup(x => x.GetCurrentBalance(It.IsAny<uint160>()))
                .Returns(Money.Coins((decimal)12.69));
            SmartContractsController controller = this.GetController();

            IActionResult result = controller.GetBalance(uint160.Zero.ToBase58Address(this.network));
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var returnString = viewResult.Value as string;
            Assert.Equal("12.69", returnString);
        }

        private SmartContractsController GetController()
        {
            return new SmartContractsController(
                null,
                null,
                null,
                null,
                null,
                this.loggerFactory.Object,
                this.network,
                this.stateRoot.Object,
                null,
                null,
                null,
                null,
                null
            );
        }
    }
}
