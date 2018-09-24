using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests.Controllers
{
    public class MiningControllerTest
    {
        private const string account = "account";
        private const string wallet = "wallet";

        private readonly Mock<IFullNode> fullNode;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public MiningControllerTest()
        {
            this.network = KnownNetworks.StratisRegTest;

            this.fullNode = new Mock<IFullNode>();
            this.fullNode.Setup(i => i.Network).Returns(this.network);

            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.fullNode.Setup(i => i.NodeService<ILoggerFactory>(false)).Returns(this.loggerFactory);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public void Generate_With_Incorrect_Block_Count_ReturnsInvalidRequest(int? blockCount)
        {
            var consensusManager = new Mock<IConsensusManager>();
            consensusManager.Setup(cm => cm.Tip).Returns(new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), new uint256(0), this.network.Consensus.LastPOWBlock - 1));
            this.fullNode.Setup(i => i.NodeService<IConsensusManager>(false)).Returns(consensusManager.Object);

            var controller = new MiningController(this.fullNode.Object);

            IActionResult result = blockCount == null ?
                controller.StartMining(new MiningRequest()) :
                controller.StartMining(new MiningRequest { BlockCount = (int)blockCount });

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
            var consensusManager = new Mock<IConsensusManager>();
            consensusManager.Setup(cm => cm.Tip).Returns(new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), new uint256(0), this.network.Consensus.LastPOWBlock - 1));
            this.fullNode.Setup(i => i.NodeService<IConsensusManager>(false)).Returns(consensusManager.Object);

            var controller = new MiningController(this.fullNode.Object);
            controller.ModelState.AddModelError("key", "error message");

            IActionResult result = controller.StartMining(new MiningRequest());

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Formatting error", error.Message);
            Assert.Equal("error message", error.Description);
        }

        [Fact]
        public void StartMining_PowNetwork_ReturnsSuccess()
        {
            var powNode = new Mock<IFullNode>();
            powNode.Setup(i => i.Network).Returns(KnownNetworks.RegTest);
            powNode.Setup(i => i.NodeService<ILoggerFactory>(false)).Returns(this.loggerFactory);

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(f => f.GetWalletsNames()).Returns(new List<string> { wallet });
            walletManager.Setup(f => f.GetAccounts(wallet)).Returns(new List<HdAccount> { new HdAccount { Name = account } });
            HdAddress address = WalletTestsHelpers.CreateAddress();
            walletManager.Setup(f => f.GetUnusedAddress(new WalletAccountReference(wallet, account))).Returns(address);
            powNode.Setup(i => i.NodeService<IWalletManager>(false)).Returns(walletManager.Object);

            var powMining = new Mock<IPowMining>();
            powMining.Setup(f => f.GenerateBlocks(It.Is<ReserveScript>(r => r.ReserveFullNodeScript == address.Pubkey), 1, int.MaxValue)).Returns(new List<uint256> { new uint256(1255632623) });
            powNode.Setup(f => f.NodeFeature<MiningFeature>(false)).Returns(new MiningFeature(KnownNetworks.RegTest, new MinerSettings(Configuration.NodeSettings.Default()), Configuration.NodeSettings.Default(), this.loggerFactory, new Mock<ITimeSyncBehaviorState>().Object, powMining.Object, null));
            powNode.Setup(i => i.NodeService<IPowMining>(false)).Returns(powMining.Object);

            var controller = new MiningController(powNode.Object);

            IActionResult result = controller.StartMining(new MiningRequest { BlockCount = 1 });

            powMining.VerifyAll();
            walletManager.VerifyAll();

            Assert.NotNull(result);
            var viewResult = Assert.IsType<JsonResult>(result);
            var resultValue = Assert.IsType<GenerateBlocksModel>(viewResult.Value);
            Assert.NotNull(resultValue);
        }

        [Fact]
        public void StartMining_PosNetwork_ConsensusTip_IsBeforeLastPowBlock_ReturnsSuccess()
        {
            var consensusManager = new Mock<IConsensusManager>();
            consensusManager.Setup(cm => cm.Tip).Returns(new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), new uint256(0), this.network.Consensus.LastPOWBlock - 1));
            this.fullNode.Setup(i => i.NodeService<IConsensusManager>(false)).Returns(consensusManager.Object);

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(f => f.GetWalletsNames()).Returns(new List<string> { wallet });
            walletManager.Setup(f => f.GetAccounts(wallet)).Returns(new List<HdAccount> { new HdAccount { Name = account } });
            HdAddress address = WalletTestsHelpers.CreateAddress();
            walletManager.Setup(f => f.GetUnusedAddress(new WalletAccountReference(wallet, account))).Returns(address);
            this.fullNode.Setup(i => i.NodeService<IWalletManager>(false)).Returns(walletManager.Object);

            var powMining = new Mock<IPowMining>();
            powMining.Setup(f => f.GenerateBlocks(It.Is<ReserveScript>(r => r.ReserveFullNodeScript == address.Pubkey), 1, int.MaxValue)).Returns(new List<uint256> { new uint256(1255632623) });
            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(false)).Returns(new MiningFeature(this.network, new MinerSettings(Configuration.NodeSettings.Default()), Configuration.NodeSettings.Default(), this.loggerFactory, new Mock<ITimeSyncBehaviorState>().Object, powMining.Object, null));

            this.fullNode.Setup(i => i.NodeService<IPowMining>(false)).Returns(powMining.Object);

            var controller = new MiningController(this.fullNode.Object);

            IActionResult result = controller.StartMining(new MiningRequest { BlockCount = 1 });

            powMining.VerifyAll();
            walletManager.VerifyAll();

            Assert.NotNull(result);
            var viewResult = Assert.IsType<JsonResult>(result);
            var resultValue = Assert.IsType<GenerateBlocksModel>(viewResult.Value);
            Assert.NotNull(resultValue);
        }

        [Fact]
        public void StartMining_PosNetwork_ConsensusTip_IsAfterLastPowBlock_ReturnsError()
        {
            var consensusManager = new Mock<IConsensusManager>();
            consensusManager.Setup(cm => cm.Tip).Returns(new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), new uint256(0), this.network.Consensus.LastPOWBlock + 1));
            this.fullNode.Setup(i => i.NodeService<IConsensusManager>(false)).Returns(consensusManager.Object);

            var controller = new MiningController(this.fullNode.Object);

            IActionResult result = controller.StartMining(new MiningRequest { BlockCount = 1 });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(405, error.Status);
            Assert.Equal("Method not allowed", error.Message);
            Assert.Equal(string.Format("This is a POS node and it's consensus tip is higher that the allowed last POW block height of {0}", this.network.Consensus.LastPOWBlock), error.Description);
        }

        [Fact]
        public void StopMining_ReturnsSuccess()
        {
            var powMining = new Mock<IPowMining>();
            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(false)).Returns(new MiningFeature(this.network, new MinerSettings(Configuration.NodeSettings.Default()), Configuration.NodeSettings.Default(), this.loggerFactory, new Mock<ITimeSyncBehaviorState>().Object, powMining.Object, null));

            var controller = new MiningController(this.fullNode.Object);

            IActionResult result = controller.StopMining();

            Assert.NotNull(result);
            var okResult = Assert.IsType<OkResult>(result);
            Assert.NotNull(okResult);
        }
    }
}