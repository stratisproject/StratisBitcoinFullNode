using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Moq;
using Breeze.Wallet.Wrappers;
using Breeze.Wallet;
using Breeze.Wallet.Controllers;
using Breeze.Wallet.Models;

namespace Breeze.Api.Tests
{
    public class ControllersTests
    {
        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            var mockWalletCreate = new Mock<IWalletWrapper>();
            mockWalletCreate.Setup(wallet => wallet.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("mnemonic");

            var controller = new WalletController(mockWalletCreate.Object);

            // Act
            var result = controller.Create(new WalletCreationModel
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            // Assert
            mockWalletCreate.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("mnemonic", viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            WalletModel walletModel = new WalletModel
            {
                FileName = "myWallet",
                Network = "MainNet",
                Addresses = new List<string> { "address1", "address2", "address3", "address4", "address5" }

            };
            var mockWalletWrapper = new Mock<IWalletWrapper>();
            mockWalletWrapper.Setup(wallet => wallet.Recover(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(walletModel);

            var controller = new WalletController(mockWalletWrapper.Object);

            // Act
            var result = controller.Recover(new WalletRecoveryModel
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = "",
                Mnemonic = "mnemonic"
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(viewResult.Value);
            Assert.IsType<WalletModel>(viewResult.Value);

            var model = viewResult.Value as WalletModel;
            Assert.Equal("myWallet", model.FileName);
        }

        [Fact]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            WalletModel walletModel = new WalletModel
            {
                FileName = "myWallet",
                Network = "MainNet",
                Addresses = new List<string> { "address1", "address2", "address3", "address4", "address5" }

            };
            var mockWalletWrapper = new Mock<IWalletWrapper>();
            mockWalletWrapper.Setup(wallet => wallet.Load(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(walletModel);

            var controller = new WalletController(mockWalletWrapper.Object);

            // Act
            var result = controller.Load(new WalletLoadModel
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(viewResult.Value);
            Assert.IsType<WalletModel>(viewResult.Value);

            var model = viewResult.Value as WalletModel;
            Assert.Equal("myWallet", model.FileName);
        }

        [Fact]
        public void FileNotFoundExceptionandReturns404()
        {
            var mockWalletWrapper = new Mock<IWalletWrapper>();
            mockWalletWrapper.Setup(wallet => wallet.Load(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            
            var controller = new WalletController(mockWalletWrapper.Object);

            // Act
            var result = controller.Load(new WalletLoadModel
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<ObjectResult>(result);
            Assert.NotNull(viewResult);		
            Assert.Equal(404, viewResult.StatusCode);
        }

    }
}
