﻿using System;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Base;
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
    public class MinerControllerTest : LogsTestBase
    {
        private MinerController controller;
        private Mock<IFullNode> fullNode;
        private Mock<IPosMinting> posMinting;
        private Mock<IWalletManager> walletManager;
        private Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;

        public MinerControllerTest()
        {
            this.fullNode = new Mock<IFullNode>();
            this.posMinting = new Mock<IPosMinting>();
            this.walletManager = new Mock<IWalletManager>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();

            this.controller = new MinerController(this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object, this.posMinting.Object);
        }

        [Fact]
        public void GetStakingInfo_WithoutPosMinting_ReturnsEmptyStakingInfoModel()
        {
            this.controller = new MinerController(this.fullNode.Object, this.LoggerFactory.Object, null);

            IActionResult response = this.controller.GetStakingInfo();

            var jsonResult = Assert.IsType<JsonResult>(response);
            var result = Assert.IsType<GetStakingInfoModel>(jsonResult.Value);
            Assert.Equal(JsonConvert.SerializeObject(new GetStakingInfoModel()), JsonConvert.SerializeObject(result));
        }

        [Fact]
        public void GetStakingInfo_WithPosMinting_ReturnsPosMintingStakingInfoModel()
        {
            this.posMinting.Setup(p => p.GetGetStakingInfoModel())
                .Returns(new GetStakingInfoModel()
                {
                    Enabled = true,
                    CurrentBlockSize = 150000
                }).Verifiable();

            IActionResult response = this.controller.GetStakingInfo();

            var jsonResult = Assert.IsType<JsonResult>(response);
            var result = Assert.IsType<GetStakingInfoModel>(jsonResult.Value);
            Assert.True(result.Enabled);
            Assert.Equal(150000, result.CurrentBlockSize);
            this.posMinting.Verify();
        }

        [Fact]
        public void GetStakingInfo_UnexpectedException_ReturnsBadRequest()
        {
            this.posMinting.Setup(p => p.GetGetStakingInfoModel())
              .Throws(new InvalidOperationException("Unable to get model"));

            IActionResult result = this.controller.GetStakingInfo();

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Unable to get model", error.Message);
        }

        [Fact]
        public void StartStaking_InvalidModelState_ReturnsBadRequest()
        {
            this.controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = this.controller.StartStaking(new StartStakingRequest());

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void StartStaking_WalletNotFound_ReturnsBadRequest()
        {
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
                .Throws(new WalletException("Wallet not found."));

            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            IActionResult result = this.controller.StartStaking(new StartStakingRequest() { Name = "myWallet" });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Wallet not found.", error.Message);
        }

        [Fact]
        public void StartStaking_InvalidWalletPassword_ReturnsBadRequest()
        {
            Wallet.Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1");
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
              .Returns(wallet);

            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            IActionResult result = this.controller.StartStaking(new StartStakingRequest() { Name = "myWallet", Password = "password2" });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Invalid password (or invalid Network)", error.Message);
        }

        [Fact]
        public void StartStaking_UnexpectedException_ReturnsBadRequest()
        {
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
                   .Throws(new InvalidOperationException("Unable to get wallet"));

            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            IActionResult result = this.controller.StartStaking(new StartStakingRequest() { Name = "myWallet" });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Unable to get wallet", error.Message);
        }

        [Fact]
        public void StartStaking_ValidWalletAndPassword_StartsStaking_ReturnsOk()
        {
            Wallet.Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1");
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
              .Returns(wallet);

            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(true))
                .Returns(new MiningFeature(Network.Main, new MinerSettings(Configuration.NodeSettings.Default()), Configuration.NodeSettings.Default(), this.LoggerFactory.Object, this.timeSyncBehaviorState.Object, null, this.posMinting.Object));

            IActionResult result = this.controller.StartStaking(new StartStakingRequest() { Name = "myWallet", Password = "password1" });

            Assert.IsType<OkResult>(result);
            this.posMinting.Verify(p => p.Stake(It.Is<PosMinting.WalletSecret>(s => s.WalletName == "myWallet" && s.WalletPassword == "password1")), Times.Exactly(1));
        }

        /// <summary>
        /// Tests that if the system time is out of symc with the rest of the network, staking doesn't start.
        /// </summary>
        [Fact]
        public void StartStaking_InvalidTimeSyncState_ReturnsBadRequest()
        {
            Wallet.Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1");
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
                .Returns(wallet);

            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);
            
            this.timeSyncBehaviorState.Setup(ts => ts.IsSystemTimeOutOfSync).Returns(true);

            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(true))
                .Returns(new MiningFeature(Network.Main, new MinerSettings(Configuration.NodeSettings.Default()), Configuration.NodeSettings.Default(), this.LoggerFactory.Object, this.timeSyncBehaviorState.Object, null, this.posMinting.Object));

            IActionResult result = this.controller.StartStaking(new StartStakingRequest() { Name = "myWallet", Password = "password1" });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Contains("Staking cannot start", error.Message);

            this.posMinting.Verify(pm => pm.Stake(It.IsAny<PosMinting.WalletSecret>()), Times.Never);
        }
    }
}