using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Moq;
using Breeze.Wallet;
using Breeze.Wallet.Controllers;
using Breeze.Wallet.Errors;
using Breeze.Wallet.Helpers;
using Breeze.Wallet.Models;
using NBitcoin;

namespace Breeze.Api.Tests
{
    public class ControllersTests
    {
        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null)).Returns(mnemonic);

            var controller = new WalletController(mockWalletCreate.Object, new Mock<ITracker>().Object);

            // Act
            var result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            // Assert
            mockWalletCreate.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(mnemonic.ToString(), viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            Wallet.Wallet wallet = new Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<DateTime>())).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object);

            // Act
            var result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<OkResult>(result);            
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            Wallet.Wallet wallet = new Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object);

            // Act
            var result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void FileNotFoundExceptionandReturns404()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            
            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object);

            // Act
            var result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = Assert.IsType<ErrorResult>(result);
            Assert.NotNull(viewResult);		
            Assert.Equal(404, viewResult.StatusCode);
        }

    }
}
