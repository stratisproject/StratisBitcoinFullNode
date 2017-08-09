using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Controllers;
using Stratis.Bitcoin.Features.WatchOnlyWallet.Models;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Tests.WatchOnlyWallet
{
    public class WatchOnlyWalletControllerTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [Trait("Module", "WatchOnlyWalletController")]
        public void Given_AddressIsNullOrEmpty_When_WatchIsCalled_Then_BadRequestIsReturned(string address)
        {
            var mockWalletManager = new Mock<IWatchOnlyWalletManager>();
            var controller = new WatchOnlyWalletController(mockWalletManager.Object);

            IActionResult result = controller.Watch(address);
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);            
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletController")]
        public void Given_ExceptionIsThrown_When_WatchIsCalled_Then_HttpConflictIsReturned()
        {
            string address = "non-null-or-empty-address";
            var mockWalletManager = new Mock<IWatchOnlyWalletManager>();
            mockWalletManager.Setup(wallet => wallet.WatchAddress(It.IsAny<string>())).Throws(new Exception());

            var controller = new WatchOnlyWalletController(mockWalletManager.Object);

            IActionResult result = controller.Watch(address);
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.Conflict, errorResult.StatusCode.Value);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletController")]
        public void Given_NoExceptionIsThrown_When_WatchIsCalled_Then_HttpOkIsReturned()
        {
            string address = "non-null-or-empty-address";
            var mockWalletManager = new Mock<IWatchOnlyWalletManager>();
            mockWalletManager.Setup(wallet => wallet.WatchAddress(It.IsAny<string>()));

            var controller = new WatchOnlyWalletController(mockWalletManager.Object);

            IActionResult result = controller.Watch(address);
            Assert.NotNull(result);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletController")]
        public void Given_ExceptionIsThrown_When_GetWatchOnlyWalletIsCalled_Then_HttpBadRequestIsReturned()
        {
            var mockWalletManager = new Mock<IWatchOnlyWalletManager>();
            mockWalletManager.Setup(wallet => wallet.GetWatchOnlyWallet()).Throws(new Exception());

            var controller = new WatchOnlyWalletController(mockWalletManager.Object);

            IActionResult result = controller.GetWatchOnlyWallet();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletController")]
        public void Given_NoExceptionIsThrown_When_GetWatchOnlyWalletIsCalled_Then_WatchOnlyWalletModelIsReturned()
        {
            var mockWalletManager = new Mock<IWatchOnlyWalletManager>();
            mockWalletManager.Setup(wallet => wallet.GetWatchOnlyWallet()).Returns(new Features.WatchOnlyWallet.WatchOnlyWallet());

            var controller = new WatchOnlyWalletController(mockWalletManager.Object);

            IActionResult result = controller.GetWatchOnlyWallet();

            mockWalletManager.VerifyAll();
            Assert.NotNull(result);
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            WatchOnlyWalletModel resultValue = Assert.IsType<WatchOnlyWalletModel>(viewResult.Value);
            Assert.NotNull(resultValue);
        }
    }
}
