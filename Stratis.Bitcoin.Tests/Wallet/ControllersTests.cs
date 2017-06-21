using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Common.JsonErrors;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.Wallet.Controllers;
using Stratis.Bitcoin.Wallet.Helpers;
using Stratis.Bitcoin.Wallet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Wallet
{
    [TestClass]
    public class ControllersTests : TestBase
    {
        [TestMethod]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(mnemonic);

            string dir = AssureEmptyDir("TestData/ControllersTests/CreateWalletSuccessfullyReturnsMnemonic");
            var dataFolder = new DataFolder(new NodeSettings {DataDir = dir});

            var controller = new WalletController(mockWalletCreate.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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
            var viewResult = result as JsonResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(mnemonic.ToString(), viewResult.Value);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);
            string dir = AssureEmptyDir("TestData/ControllersTests/LoadWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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
            var viewResult = result as OkResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(200, viewResult.StatusCode);
        }

        [TestMethod]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);
            string dir = AssureEmptyDir("TestData/ControllersTests/RecoverWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            // Act
            var result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = result as OkResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(200, viewResult.StatusCode);
        }

        [TestMethod]
        public void FileNotFoundExceptionandReturns404()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            string dir = AssureEmptyDir("TestData/ControllersTests/FileNotFoundExceptionandReturns404");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            // Act
            var result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockWalletWrapper.VerifyAll();
            var viewResult = result as ErrorResult;
            Assert.IsNotNull(viewResult);
            Assert.AreEqual(404, viewResult.StatusCode);
        }        
    }
}
