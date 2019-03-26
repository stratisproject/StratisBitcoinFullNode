using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreControllerTests
    {
        private const string ValidHash = "09d889192a45ba033d4fb886d7aa62bd19b36697211b3d02ac254cf47e2326b0";

        private const string BlockAsHex =
            "07000000867ccd8f8b21f48e1423d2217fdfe0ea5108dcd6f3371933d584e8f250f5c6600fdf4ccef23cbdb6d81e6bde" +
            "a2f0f45aca69a35a9817590c60b5a4ce4a44d1cc30c644592060041a00000000020100000030c6445901000000000000" +
            "0000000000000000000000000000000000000000000000000000ffffffff03029423ffffffff01000000000000000000" +
            "000000000100000030c6445901795088bf033121a794ea35a11d39dbcd2495b64756e6de76d86944fdeea4ddbc020000" +
            "00484730440220096615c8fdec79ecf477cea2104859f7db98ed883f242b08fef316e3abd41a30022070d82dd743eeed" +
            "324e90cb3c168144031ba8c8b14a6af167b98253614be3d23c01ffffffff0300000000000000000000011f4a8b000000" +
            "232102e89f4f5ac02d3e5f9114253470838ee73c9ba507262ba4db7f0b3f840cf0e1d3ac40432e4a8b000000232102e8" +
            "9f4f5ac02d3e5f9114253470838ee73c9ba507262ba4db7f0b3f840cf0e1d3ac00000000463044022002efd3facb7bc9" +
            "9407d0f7c6b9c8e80898608f63f3141b06371bbd5e762dd4ab02204f1a5e8cca1a70a5b6dee55746f100042e3479c291" +
            "68dd9970c1b3147cbd6ed8";

        private const string InvalidHash = "This hash is no good";

        [Fact]
        public void GetBlock_With_null_Hash_IsInvalid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = null,
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_empty_Hash_IsInvalid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = "",
                OutputJson = false
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeFalse();
        }

        [Fact]
        public void GetBlock_With_good_Hash_IsValid()
        {
            var requestWithNoHash = new SearchByHashRequest()
            {
                Hash = "some good hash",
                OutputJson = true
            };
            var validationContext = new ValidationContext(requestWithNoHash);
            Validator.TryValidateObject(requestWithNoHash, validationContext, null, true).Should().BeTrue();
        }

        [Fact]
        public void Get_Block_When_Hash_Is_Not_Found_Should_Return_Not_Found_Object_Result()
        {
            (Mock<IBlockStore> store, BlockStoreController controller) = GetControllerAndStore();

            store.Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult((Block)null));

            Task<IActionResult> response = controller.GetBlockAsync(new SearchByHashRequest()
            { Hash = ValidHash, OutputJson = true });

            response.Result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundObjectResult = (NotFoundObjectResult)response.Result;
            notFoundObjectResult.StatusCode.Should().Be(404);
            notFoundObjectResult.Value.Should().Be("Block not found");
        }

        [Fact]
        public void Get_Block_When_Hash_Is_Invalid_Should_Error_With_Explanation()
        {
            (Mock<IBlockStore> store, BlockStoreController controller) = GetControllerAndStore();

            Task<IActionResult> response = controller.GetBlockAsync(new SearchByHashRequest()
            { Hash = InvalidHash, OutputJson = true });

            response.Result.Should().BeOfType<ErrorResult>();
            var notFoundObjectResult = (ErrorResult)response.Result;
            notFoundObjectResult.StatusCode.Should().Be(400);
            ((ErrorResponse)notFoundObjectResult.Value).Errors[0]
                .Description.Should().Contain("Invalid Hex String");
        }

        [Fact]
        public void Get_Block_When_Block_Is_Found_And_Requesting_JsonOuput()
        {
            (Mock<IBlockStore> store, BlockStoreController controller) = GetControllerAndStore();

            store.Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult(Block.Parse(BlockAsHex, KnownNetworks.StratisTest)));

            Task<IActionResult> response = controller.GetBlockAsync(new SearchByHashRequest()
            { Hash = ValidHash, OutputJson = true });

            response.Result.Should().BeOfType<JsonResult>();
            var result = (JsonResult)response.Result;

            result.Value.Should().BeOfType<BlockModel>();
            ((BlockModel)result.Value).Hash.Should().Be(ValidHash);
            ((BlockModel)result.Value).MerkleRoot.Should()
                .Be("ccd1444acea4b5600c5917985aa369ca5af4f0a2de6b1ed8b6bd3cf2ce4cdf0f");
        }

        [Fact]
        public void Get_Block_When_Block_Is_Found_And_Requesting_Verbose_JsonOuput()
        {
            (Mock<IBlockStore> store, BlockStoreController controller) = GetControllerAndStore();

            store
                .Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult(Block.Parse(BlockAsHex, KnownNetworks.StratisTest)));

            Task<IActionResult> response = controller.GetBlockAsync(new SearchByHashRequest() { Hash = ValidHash, OutputJson = true, ShowTransactionDetails = true });

            response.Result.Should().BeOfType<JsonResult>();
            var result = (JsonResult)response.Result;

            result.Value.Should().BeOfType<BlockTransactionDetailsModel>();
            ((BlockTransactionDetailsModel)result.Value).Transactions.Should().HaveCountGreaterThan(1);
        }

        [Fact]
        public void Get_Block_When_Block_Is_Found_And_Requesting_RawOuput()
        {
            (Mock<IBlockStore> store, BlockStoreController controller) = GetControllerAndStore();

            store
                .Setup(c => c.GetBlockAsync(It.IsAny<uint256>()))
                .Returns(Task.FromResult(Block.Parse(BlockAsHex, KnownNetworks.StratisTest)));

            Task<IActionResult> response = controller.GetBlockAsync(new SearchByHashRequest() { Hash = ValidHash, OutputJson = false });

            response.Result.Should().BeOfType<JsonResult>();
            var result = (JsonResult)response.Result;
            ((Block)(result.Value)).ToHex(KnownNetworks.StratisTest).Should().Be(BlockAsHex);
        }

        [Fact]
        public void GetBlockCount_ReturnsHeightFromChainState()
        {
            var logger = new Mock<ILoggerFactory>();
            var store = new Mock<IBlockStore>();
            var chainState = new Mock<IChainState>();
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisTest);

            logger.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>);

            chainState.Setup(c => c.ConsensusTip)
                .Returns(chain.GetBlock(2));

            var controller = new BlockStoreController(KnownNetworks.StratisTest, logger.Object, store.Object, chainState.Object, chain);

            var json = (JsonResult)controller.GetBlockCount();
            int result = int.Parse(json.Value.ToString());

            Assert.Equal(2, result);
        }

        private static (Mock<IBlockStore> store, BlockStoreController controller) GetControllerAndStore()
        {
            var logger = new Mock<ILoggerFactory>();
            var store = new Mock<IBlockStore>();
            var chainState = new Mock<IChainState>();

            logger.Setup(l => l.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>);

            var chain = new Mock<ConcurrentChain>();
            Block block = Block.Parse(BlockAsHex, KnownNetworks.StratisTest);
            chain.Setup(c => c.GetBlock(It.IsAny<uint256>())).Returns(new ChainedHeader(block.Header, block.Header.GetHash(), 1));
            chain.Setup(x => x.Tip).Returns(new ChainedHeader(block.Header, block.Header.GetHash(), 1));

            var controller = new BlockStoreController(KnownNetworks.StratisTest, logger.Object, store.Object, chainState.Object, chain.Object);

            return (store, controller);
        }
    }
}