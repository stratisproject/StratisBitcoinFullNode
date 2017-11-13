using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletControllerTest : LogsTestBase
    {
        [Fact]
        public void GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithDifferentWordCountCreatesMnemonicWithCorrectNumberOfWords()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithDifferentWordCountCreatesMnemonicWithCorrectNumberOfWords");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic(wordCount: 24);

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(24, resultingWords.Length);
        }

        [Fact]
        public void GenerateMnemonicWithStrangeLanguageCasingReturnsCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithStrangeLanguageCasingReturnsCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("eNgLiSh");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithEnglishWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithEnglishWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("english");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithFrenchWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithFrenchWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("french");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.French.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithSpanishWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithSpanishWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("spanish");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.Spanish.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithJapaneseWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithJapaneseWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("japanese");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            // japanese uses a JP space symbol.
            string[] resultingWords = (viewResult.Value as string).Split('　');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.Japanese.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseTraditionalWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithChineseTraditionalWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesetraditional");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.ChineseTraditional.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseSimplifiedWordListCreatesCorrectMnemonic()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithChineseSimplifiedWordListCreatesCorrectMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesesimplified");

            JsonResult viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                var index = -1;
                Assert.True(Wordlist.ChineseSimplified.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithUnknownLanguageReturnsBadRequest()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithUnknownLanguageReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("invalidlanguage");


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
            Assert.Equal("Invalid language 'invalidlanguage'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.", error.Message);
        }

        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(mnemonic);

            string dir = AssureEmptyDir("TestData/WalletControllerTest/CreateWalletSuccessfullyReturnsMnemonic");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });


            mockWalletCreate.VerifyAll();
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(mnemonic.ToString(), viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateWalletWithInvalidModelStateReturnsBadRequest()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();

            string dir = AssureEmptyDir("TestData/WalletControllerTest/CreateWalletWithInvalidModelStateReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Name cannot be empty.");


            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "",
                FolderPath = "",
                Password = "",
                Network = ""
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Name cannot be empty.", error.Message);
        }

        [Fact]
        public void CreateWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new WalletException(errorMessage));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/CreateWalletWithInvalidOperationExceptionReturnsConflict");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });


            mockWalletCreate.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.Equal(errorMessage, error.Message);
        }

        [Fact]
        public void CreateWalletWithNotSupportedExceptionExceptionReturnsBadRequest()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new NotSupportedException("Not supported"));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/CreateWalletWithInvalidOperationExceptionReturnsConflict");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });


            mockWalletCreate.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("There was a problem creating a wallet.", error.Message);
        }

        [Fact]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            Bitcoin.Features.Wallet.Wallet wallet = new Bitcoin.Features.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);
            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });


            mockWalletWrapper.VerifyAll();
            OkResult viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void RecoverWalletWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithInvalidModelStateReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");


            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void RecoverWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new WalletException(errorMessage));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithInvalidOperationExceptionReturnsConflict");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.Equal(errorMessage, error.Message);
        }

        [Fact]
        public void RecoverWalletWithFileNotFoundExceptionReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FileNotFoundException("File not found."));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithFileNotFoundExceptionReturnsNotFound");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(404, error.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", error.Description);
            Assert.Equal("Wallet not found.", error.Message);
        }

        [Fact]
        public void RecoverWalletWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FormatException("Formatting failed."));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithExceptionReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = "",
                Network = "MainNet",
                Mnemonic = "mnemonic"
            });

            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
            Assert.Equal("Formatting failed.", error.Message);
        }

        [Fact]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            Bitcoin.Features.Wallet.Wallet wallet = new Bitcoin.Features.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet")
            };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });

            mockWalletWrapper.VerifyAll();
            OkResult viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void LoadWalletWithInvalidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletWithInvalidModelReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");


            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void LoadWalletWithFileNotFoundExceptionandReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletWithFileNotFoundExceptionandReturnsNotFound");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(404, error.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", error.Description);
            Assert.Equal("This wallet was not found at the specified location.", error.Message);
        }

        [Fact]
        public void LoadWalletWithSecurityExceptionandReturnsForbidden()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<SecurityException>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletWithSecurityExceptionandReturnsForbidden");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(403, error.Status);
            Assert.StartsWith("System.Security.SecurityException", error.Description);
            Assert.Equal("Wrong password, please try again.", error.Message);
        }

        [Fact]
        public void LoadWalletWithOtherExceptionandReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FormatException>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletWithOtherExceptionandReturnsBadRequest");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetGeneralInfoSuccessfullyReturnsWalletGeneralInfoModel()
        {
            Bitcoin.Features.Wallet.Wallet wallet = new Bitcoin.Features.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet"),
                CreationTime = new DateTime(2017, 6, 19, 1, 1, 1),
                AccountsRoot = new List<AccountRoot> {
                    new AccountRoot()
                    {
                        CoinType = (CoinType)Network.Main.Consensus.CoinType,
                        LastBlockSyncedHeight = 15
                    }
                }
            };

            var concurrentChain = new ConcurrentChain(Network.Main);
            ChainedBlock tip = WalletTestsHelpers.AppendBlock(null, new [] {concurrentChain});

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedNodes)
                .Returns(new NodesCollection());

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, Network.Main, concurrentChain, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });


            mockWalletWrapper.VerifyAll();
            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            WalletGeneralInfoModel resultValue = Assert.IsType<WalletGeneralInfoModel>(viewResult.Value);

            Assert.Equal(wallet.Network, resultValue.Network);
            Assert.Equal(wallet.CreationTime, resultValue.CreationTime);
            Assert.Equal(15, resultValue.LastBlockSyncedHeight);
            Assert.Equal(0, resultValue.ConnectedNodes);
            Assert.Equal(tip.Height, resultValue.ChainTip);
            Assert.True(resultValue.IsDecrypted);
        }

        [Fact]
        public void GetGeneralInfoWithModelStateErrorReturnsBadRequest()
        {
            Bitcoin.Features.Wallet.Wallet wallet = new Bitcoin.Features.Wallet.Wallet
            {
                Name = "myWallet",
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Invalid name.");


            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Invalid name.", error.Message);
        }

        [Fact]
        public void GetGeneralInfoWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Throws<FormatException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);


            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetHistoryWithoutAddressesReturnsEmptyModel()
        {
            var walletName = "myWallet";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName)).Returns(new List<FlatHistory>());
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Empty(model.TransactionsHistory);
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            var walletName = "myWallet";
            HdAddress address = WalletTestsHelpers.CreateAddress();
            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> {address};
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = addresses
                } }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName)).Returns(flat);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Single(model.TransactionsHistory);
            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[0];

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(1, resultingTransactionModel.ConfirmedInBlock);
        }

        [Fact]
        public void GetHistoryWithValidModelWithTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            var walletName = "myWallet";
            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> {address, changeAddress};
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = addresses
                } }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName)).Returns(flat);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(2, model.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[0];

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = model.TransactionsHistory[1];

            Assert.Equal(TransactionItemType.Send, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails.Amount, resultingPayment.Amount);
        }

        [Fact]
        public void GetHistoryWithValidModelWithFeeBelowZeroSetsFeeToZero()
        {
            var walletName = "myWallet";

            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(310000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> {address, changeAddress};

            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = addresses
                } }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName)).Returns(flat);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(2, model.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[1];
            Assert.Equal(0, resultingTransactionModel.Fee);
        }

        /// <summary>
        /// Tests that when a transaction has been sent that has multiple inputs to form the transaction these duplicate spending details do not show up multiple times in the history.
        /// </summary>
        [Fact]
        public void GetHistoryWithDuplicateSpentTransactionsSelectsDistinctsSpentTransactionsForDuplicates()
        {
            var walletName = "myWallet";
            var addresses = new List<HdAddress>
            {
                new HdAddress
                {
                    HdPath = $"m/44'/0'/0'/1/0",
                    Transactions = new List<TransactionData>
                    {
                        new TransactionData
                        {
                            Id = new uint256(13),
                            Amount = new Money(50),
                            BlockHeight = 5,
                            SpendingDetails = new SpendingDetails
                            {
                                TransactionId = new uint256(15),
                                BlockHeight = 10,
                                Payments = new List<PaymentDetails>
                                {
                                    new PaymentDetails
                                    {
                                        Amount = new Money(80),
                                        DestinationAddress = "address1"
                                    }
                                }
                            }
                        }
                    }
                },
                new HdAddress
                {
                    HdPath = $"m/44'/0'/0'/1/1",
                    Transactions = new List<TransactionData>
                    {
                        new TransactionData
                        {
                            Id = new uint256(14),
                            Amount = new Money(30),
                            BlockHeight = 6,
                            SpendingDetails = new SpendingDetails
                            {
                                TransactionId = new uint256(15),
                                BlockHeight = 10,
                                Payments = new List<PaymentDetails>
                                {
                                    new PaymentDetails
                                    {
                                        Amount = new Money(80),
                                        DestinationAddress = "address1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = addresses
                } }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);
            mockWalletManager.Setup(w => w.GetHistory(walletName)).Returns(flat);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });


            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Single(model.TransactionsHistory);

            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[0];

            Assert.Equal(TransactionItemType.Send, resultingTransactionModel.Type);
            Assert.Equal(new uint256(15), resultingTransactionModel.Id);
            Assert.Equal(10, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(new Money(80), resultingTransactionModel.Amount);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal("address1", resultingPayment.DestinationAddress);
            Assert.Equal(new Money(80), resultingPayment.Amount);
        }

        [Fact]
        public void GetHistoryWithExceptionReturnsBadRequest()
        {
            var walletName = "myWallet";
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet")).Throws(new InvalidOperationException("Issue retrieving wallets."));
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving wallets.", error.Message);
        }

        [Fact]
        public void GetHistoryWithChangeAddressesShouldIncludeSpentChangeAddesses()
        {
            string walletName = "myWallet";

            // create addresses
            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress changeAddress2 = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress2 = WalletTestsHelpers.CreateAddress();

            // create transaction on change address
            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            // create transaction with spending details
            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);
            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            // create transaction on change address
            TransactionData changeTransaction2 = WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(200000), 2);
            changeAddress2.Transactions.Add(changeTransaction2);

            // create transaction with spending details on change address
            PaymentDetails paymentDetails2 = WalletTestsHelpers.CreatePaymentDetails(new Money(50000), destinationAddress2);
            SpendingDetails spendingDetails2 = WalletTestsHelpers.CreateSpendingDetails(changeTransaction2, paymentDetails2);
            TransactionData transaction2 = WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(275000), 2, spendingDetails2);
            changeAddress.Transactions.Add(transaction2);

            var addresses = new List<HdAddress> {address, changeAddress, changeAddress2};
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = addresses
                } }
            });

            List<FlatHistory> flat = addresses.SelectMany(s =>s.Transactions.Select(t => new FlatHistory {Address = s, Transaction = t})).ToList();

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory(walletName)).Returns(flat);
            mockWalletWrapper.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(3, model.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[0];

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = model.TransactionsHistory[1];

            Assert.Equal(TransactionItemType.Send, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            PaymentDetailModel resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails.Amount, resultingPayment.Amount);

            resultingTransactionModel = model.TransactionsHistory[2];

            Assert.Equal(TransactionItemType.Send, resultingTransactionModel.Type);
            Assert.Null(resultingTransactionModel.ToAddress);
            Assert.Equal(spendingDetails2.TransactionId, resultingTransactionModel.Id);
            Assert.Equal(spendingDetails2.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(spendingDetails2.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Equal(paymentDetails2.Amount, resultingTransactionModel.Amount);
            Assert.Equal(new Money(25000), resultingTransactionModel.Fee);

            Assert.Equal(1, resultingTransactionModel.Payments.Count);
            resultingPayment = resultingTransactionModel.Payments.ElementAt(0);
            Assert.Equal(paymentDetails2.DestinationAddress, resultingPayment.DestinationAddress);
            Assert.Equal(paymentDetails2.Amount, resultingPayment.Amount);
        }

        [Fact]
        public void GetBalanceWithValidModelStateReturnsWalletBalanceModel()
        {
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 2));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            HdAccount account2 = WalletTestsHelpers.CreateAccount("account 2");
            HdAddress account2Address1 = WalletTestsHelpers.CreateAddress();
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(5), new Money(74000), null));
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(6), new Money(18700), 3));

            HdAddress account2Address2 = WalletTestsHelpers.CreateAddress();
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(7), new Money(65000), null));
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(8), new Money(89300), 4));

            account2.ExternalAddresses.Add(account2Address1);
            account2.InternalAddresses.Add(account2Address2);

            var accounts = new List<HdAccount> { account, account2 };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetAccounts("myWallet"))
                .Returns(accounts);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(2, model.AccountsBalances.Count);

            AccountBalance resultingBalance = model.AccountsBalances[0];
            Assert.Equal(Network.Main.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account.Name, resultingBalance.Name);
            Assert.Equal(account.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);

            resultingBalance = model.AccountsBalances[1];
            Assert.Equal(Network.Main.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account2.Name, resultingBalance.Name);
            Assert.Equal(account2.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
        }

        [Fact]
        public void GetBalanceWithEmptyListOfAccountsReturnsWalletBalanceModel()
        {
            var accounts = new List<HdAccount>();
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetAccounts("myWallet"))
                .Returns(accounts);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Empty(model.AccountsBalances);
        }

        [Fact]
        public void GetBalanceWithInvalidValidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Message);
        }

        [Fact]
        public void GetBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetAccounts("myWallet"))
                  .Throws(new InvalidOperationException("Issue retrieving accounts."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving accounts.", error.Message);
        }

        [Fact]
        public void BuildTransactionWithValidRequestAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithValidRequestNotAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Message);
        }

        [Fact]
        public void BuildTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();

            var key = new Key();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue building transaction.", error.Message);
        }

        [Fact]
        public void SendTransactionSuccessfulReturnsOkResponse()
        {
            var mockWalletWrapper = new Mock<IBroadcasterManager>();
            mockWalletWrapper.Setup(m => m.TryBroadcastAsync(It.IsAny<Transaction>()))
                .Returns(Task.FromResult(Success.Yes));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), mockWalletWrapper.Object, DateTimeProvider.Default);
            IActionResult result = controller.SendTransactionAsync(new SendTransactionRequest
            {
                Hex = new uint256(15555).ToString()
            }).GetAwaiter().GetResult();

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void SendTransactionFailedReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IBroadcasterManager>();
            mockWalletWrapper.Setup(m => m.TryBroadcastAsync(It.IsAny<Transaction>()))
                .Returns(Task.FromResult(Success.No));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), mockWalletWrapper.Object, DateTimeProvider.Default);
            IActionResult result = controller.SendTransactionAsync(new SendTransactionRequest
            {
                Hex = new uint256(15555).ToString()
            }).GetAwaiter().GetResult();

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.NotNull(error.Message);
        }

        [Fact]
        public void SendTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Hex", "Hex required.");
            IActionResult result = controller.SendTransactionAsync(new SendTransactionRequest
            {
                Hex = ""
            }).GetAwaiter().GetResult();

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Hex required.", error.Message);
        }

        [Fact]
        public void SendTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IBroadcasterManager>();
            mockWalletWrapper.Setup(m => m.TryBroadcastAsync(It.IsAny<Transaction>()))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), mockWalletWrapper.Object, DateTimeProvider.Default);

            IActionResult result = controller.SendTransactionAsync(new SendTransactionRequest
            {
                Hex = new uint256(15555).ToString()
            }).GetAwaiter().GetResult();

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue building transaction.", error.Message);
        }

        [Fact]
        public void ListWalletFilesWithExistingWalletFilesReturnsWalletFileModel()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/ListWalletFilesWithExistingWalletFilesReturnsWalletFileModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Returns((dataFolder.WalletPath, new[] { "wallet1.wallet.json", "wallet2.wallet.json" }));

            //var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletFileExtension()).Returns("wallet.json");

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(dir, model.WalletsPath);
            Assert.Equal(2, model.WalletsFiles.Count());
            Assert.EndsWith("wallet1.wallet.json", model.WalletsFiles.ElementAt(0));
            Assert.EndsWith("wallet2.wallet.json", model.WalletsFiles.ElementAt(1));
        }

        [Fact]
        public void ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Returns((dataFolder.WalletPath, Enumerable.Empty<string>()));

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(dataFolder.WalletPath, model.WalletsPath);
            Assert.Empty(model.WalletsFiles);
        }

        [Fact]
        public void ListWalletFilesWithExceptionReturnsBadRequest()
        {
            var dataFolder = new DataFolder(new NodeSettings { DataDir = "" });

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Throws(new Exception("something happened."));

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("something happened.", error.Message);
        }

        [Fact]
        public void CreateNewAccountWithValidModelReturnsAccountName()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Returns(new HdAccount { Name = "Account 1" });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("Account 1", viewResult.Value as string);
        }

        [Fact]
        public void CreateNewAccountWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void CreateNewAccountWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }

        [Fact]
        public void GetUnusedAddressWithValidModelReturnsUnusedAddress()
        {
            HdAddress address = WalletTestsHelpers.CreateAddress();
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Returns(address);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(address.Address, viewResult.Value as string);
        }

        [Fact]
        public void GetUnusedAddressWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("AccountName", "An account name is required.");

            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("An account name is required.", error.Message);
        }

        [Fact]
        public void GetUnusedAddressWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }

        [Fact]
        public void GetMaximumBalanceWithValidModelStateReturnsMaximumBalance()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Error in model", "There was an error in the model.");
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
            Assert.Equal("There was an error in the model.", error.Message);

        }

        [Fact]
        public void GetMaximumBalanceSuccessfullyReturnsMaximumBalanceAndFee()
        {
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            mockWalletTransactionHandler.Setup(w => w.GetMaximumSpendableAmount(It.IsAny<WalletAccountReference>(), It.IsAny<FeeType>(), true)).Returns((new Money(1000000), new Money(100)));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as MaxSpendableAmountModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(1000000), model.MaxSpendableAmount);
            Assert.Equal(new Money(100), model.Fee);
        }

        [Fact]
        public void GetMaximumBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            mockWalletTransactionHandler.Setup(w => w.GetMaximumSpendableAmount(It.IsAny<WalletAccountReference>(), It.IsAny<FeeType>(), true)).Throws(new Exception("failure"));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
        }

        [Fact]
        public void GetTransactionFeeEstimateWithValidRequestReturnsFee()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            Key key = new Key();
            Money expectedFee = new Money(1000);
            mockWalletTransactionHandler.Setup(m => m.EstimateFee(It.IsAny<TransactionBuildContext>()))
                .Returns(expectedFee);

            WalletController controller = new WalletController(this.LoggerFactory.Object, mockWalletWrapper.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>(), new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetTransactionFeeEstimate(new TxFeeEstimateRequest
            {
                AccountName = "Account 1",
                Amount = new Money(150000).ToString(),
                DestinationAddress = key.PubKey.GetAddress(Network.Main).ToString(),
                FeeType = "105",
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            Money actualFee = viewResult.Value as Money;

            Assert.NotNull(actualFee);
            Assert.Equal(expectedFee, actualFee);
        }
    }
}
