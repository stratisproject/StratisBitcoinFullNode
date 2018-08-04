using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests.Controllers
{
    public class MiningApiControllerTest : LogsTestBase
    {
        private MiningApiController apiController;
        private readonly Mock<IPowMining> powMining;
        private readonly Mock<IWalletManager> walletManager;

        public MiningApiControllerTest()
        {
            this.powMining = new Mock<IPowMining>();
            this.walletManager = new Mock<IWalletManager>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public void Generate_With_Incorrect_Block_Count_ReturnsInvalidRequest(int? blockCount)
        {
            this.apiController = new MiningApiController(this.powMining.Object, this.LoggerFactory.Object, this.walletManager.Object);

            IActionResult result = blockCount == null ? 
                this.apiController.Generate(new MiningRequest()) : 
                this.apiController.Generate(new MiningRequest { BlockCount = (int)blockCount });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(403, error.Status);
            Assert.Equal("Invalid request", error.Message);
            Assert.Equal("The number of blocks to mine must be higher than zero.", error.Description);
        }

        [Fact]
        public void Generate_Blocks_When_Model_Is_Invalid_ReturnsBadRequest()
        {
            this.apiController = new MiningApiController(this.powMining.Object, this.LoggerFactory.Object, this.walletManager.Object);

            this.apiController.ModelState.AddModelError("key", "error message");

            IActionResult result = this.apiController.Generate(new MiningRequest());

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Formatting error", error.Message);
            Assert.Equal("error message", error.Description);
        }

        [Fact]
        public void Generate_Blocks_ReturnsSuccess()
        {
            const string wallet = "wallet";
            const string account = "account";

            this.walletManager.Setup(w => w.GetWalletsNames()).Returns(new List<string> { wallet });
            this.walletManager.Setup(w => w.GetAccounts(wallet)).Returns(new List<HdAccount> { new HdAccount { Name = account } });

            HdAddress address = WalletTestsHelpers.CreateAddress();
            this.walletManager.Setup(w => w.GetUnusedAddress(new WalletAccountReference(wallet, account))).Returns(address);

            this.powMining.Setup(p => p.GenerateBlocks(It.Is<ReserveScript>(r => r.ReserveFullNodeScript == address.Pubkey), 1, int.MaxValue))
                .Returns(new List<uint256> { new uint256(1255632623) });

            this.apiController = new MiningApiController(this.powMining.Object, this.LoggerFactory.Object, this.walletManager.Object);

            IActionResult result = this.apiController.Generate(new MiningRequest { BlockCount = 1 });

            this.walletManager.VerifyAll();
            this.powMining.VerifyAll();

            Assert.NotNull(result);
            var viewResult = Assert.IsType<JsonResult>(result);
            var resultValue = Assert.IsType<GenerateBlocksModel>(viewResult.Value);
            Assert.NotNull(resultValue);
        }     
    }
}