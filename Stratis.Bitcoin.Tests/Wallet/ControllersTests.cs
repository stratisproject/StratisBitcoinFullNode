using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Common.JsonErrors;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.Wallet.Controllers;
using Stratis.Bitcoin.Wallet.Helpers;
using Stratis.Bitcoin.Wallet.Models;
using Xunit;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class ControllersTests
    {
        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null)).Returns(mnemonic);
            
            var controller = new WalletController(mockWalletCreate.Object, new Mock<ITracker>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object);

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
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object);

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
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object);

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
            
            var controller = new WalletController(mockWalletWrapper.Object, new Mock<ITracker>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object);

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
