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
using Xunit;
using System.Collections.Generic;
using NBitcoin.Protocol;
using System.Security;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Tests.Logging;
using System.Linq;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class WalletControllerTest : LogsTestBase
    {
        [Fact]
        public void GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

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

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            IActionResult result = controller.GenerateMnemonic("invalidlanguage");


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

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

            var controller = new WalletController(mockWalletCreate.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);
            
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

            var controller = new WalletController(mockWalletCreate.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Name cannot be empty.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void CreateWalletWithInvalidOperationExceptionReturnsConflict()
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Invalid operation"));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/CreateWalletWithInvalidOperationExceptionReturnsConflict");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletCreate.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("This wallet already exists.", error.Message);
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

            var controller = new WalletController(mockWalletCreate.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.NotSupportedException", error.Description);
            Assert.Equal("There was a problem creating a wallet.", error.Message);
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
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);
            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void RecoverWalletWithInvalidOperationExceptionReturnsConflict()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new InvalidOperationException("Invalid operation."));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithInvalidOperationExceptionReturnsConflict");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("This wallet already exists.", error.Message);
        }

        [Fact]
        public void RecoverWalletWithFileNotFoundExceptionReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FileNotFoundException("File not found."));

            string dir = AssureEmptyDir("TestData/WalletControllerTest/RecoverWalletWithFileNotFoundExceptionReturnsNotFound");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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
            Assert.Equal(1, errorResponse.Errors.Count);

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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
            Assert.Equal("Formatting failed.", error.Message);
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
            mockWalletWrapper.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletSuccessfullyReturnsWalletModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);
            controller.ModelState.AddModelError("Password", "A password is required.");

            
            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                FolderPath = "",
                Password = ""
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void LoadWalletWithFileNotFoundExceptionandReturnsNotFound()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            string dir = AssureEmptyDir("TestData/WalletControllerTest/LoadWalletWithFileNotFoundExceptionandReturnsNotFound");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            
            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetGeneralInfoSuccessfullyReturnsWalletGeneralInfoModel()
        {
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
                Network = WalletHelpers.GetNetwork("mainnet"),
                CreationTime = new DateTime(2017, 6, 19, 1, 1, 1),
                AccountsRoot = new List<AccountRoot>() {
                    new AccountRoot()
                    {
                        CoinType = (CoinType)Network.Main.Consensus.CoinType,
                        LastBlockSyncedHeight = 15
                    }
                }
            };

            var concurrentChain = new ConcurrentChain(Network.Main);
            ChainedBlock tip = AppendBlock(concurrentChain);

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedNodes)
                .Returns(new NodesCollection());

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, Network.Main, concurrentChain, It.IsAny<DataFolder>());

            
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
            Bitcoin.Wallet.Wallet wallet = new Bitcoin.Wallet.Wallet
            {
                Name = "myWallet",
            };

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("Name", "Invalid name.");

            
            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Invalid name.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void GetGeneralInfoWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetWallet("myWallet")).Throws<FormatException>();

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());

            
            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });


            mockWalletWrapper.VerifyAll();
            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetHistoryWithoutAddressesReturnsEmptyModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet"))
                .Returns(new List<HdAddress>());

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetHistory(new WalletHistoryRequest()
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(0, model.TransactionsHistory.Count);
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            HdAddress address = CreateAddress();
            TransactionData transaction = CreateTransaction(new uint256(1), new Money(500000), 1);
            address.Transactions.Add(transaction);

            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet"))
                .Returns(new List<HdAddress>() { address });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetHistory(new WalletHistoryRequest()
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(1, model.TransactionsHistory.Count);
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
            HdAddress changeAddress = CreateAddress(changeAddress: true);
            HdAddress address = CreateAddress();
            HdAddress destinationAddress = CreateAddress();

            TransactionData changeTransaction = CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);


            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet"))
                .Returns(new List<HdAddress>() { address, changeAddress });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetHistory(new WalletHistoryRequest()
            {
                WalletName = "myWallet"
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
            HdAddress changeAddress = CreateAddress(changeAddress: true);
            HdAddress address = CreateAddress();
            HdAddress destinationAddress = CreateAddress();

            TransactionData changeTransaction = CreateTransaction(new uint256(2), new Money(310000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);


            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet"))
                .Returns(new List<HdAddress>() { address, changeAddress });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetHistory(new WalletHistoryRequest()
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);

            Assert.Equal(2, model.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = model.TransactionsHistory[1];
            Assert.Equal(0, resultingTransactionModel.Fee);
        }

        [Fact]
        public void GetHistoryWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetHistory("myWallet"))
                .Throws(new InvalidOperationException("Issue retrieving wallets."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetHistory(new WalletHistoryRequest()
            {
                WalletName = "myWallet"
            });


            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving wallets.", error.Message);
        }

        [Fact]
        public void GetBalanceWithValidModelStateReturnsWalletBalanceModel()
        {
            HdAccount account = CreateAccount("account 1");
            HdAddress accountAddress1 = CreateAddress();
            accountAddress1.Transactions.Add(CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(CreateTransaction(new uint256(2), new Money(10000), 1));

            HdAddress accountAddress2 = CreateAddress();
            accountAddress2.Transactions.Add(CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(CreateTransaction(new uint256(4), new Money(120000), 2));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            HdAccount account2 = CreateAccount("account 2");
            HdAddress account2Address1 = CreateAddress();
            account2Address1.Transactions.Add(CreateTransaction(new uint256(5), new Money(74000), null));
            account2Address1.Transactions.Add(CreateTransaction(new uint256(6), new Money(18700), 3));

            HdAddress account2Address2 = CreateAddress();
            account2Address2.Transactions.Add(CreateTransaction(new uint256(7), new Money(65000), null));
            account2Address2.Transactions.Add(CreateTransaction(new uint256(8), new Money(89300), 4));

            account2.ExternalAddresses.Add(account2Address1);
            account2.InternalAddresses.Add(account2Address2);

            var accounts = new List<HdAccount>() { account, account2 };
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(w => w.GetAccounts("myWallet"))
                .Returns(accounts);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetBalance(new WalletBalanceRequest()
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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetBalance(new WalletBalanceRequest()
            {
                WalletName = "myWallet"
            });

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(0, model.AccountsBalances.Count);            
        }

        [Fact]
        public void GetBalanceWithInvalidValidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.GetBalance(new WalletBalanceRequest()
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void GetBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetAccounts("myWallet"))
                  .Throws(new InvalidOperationException("Issue retrieving accounts."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetBalance(new WalletBalanceRequest()
            {
                WalletName = "myWallet"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving accounts.", error.Message);
        }

        [Fact]
        public void BuildTransactionWithValidRequestAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var key = new Key();
            mockWalletWrapper.Setup(m => m.BuildTransaction("myWallet", "Account 1", "test", key.PubKey.GetAddress(Network.Main).ToString(), new Money(150000), FeeType.High, 0))
                .Returns(("hexString", new uint256(15), new Money(150)));


            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest()
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
            Assert.Equal("hexString", model.Hex);
            Assert.Equal(new Money(150), model.Fee);
            Assert.Equal(new uint256(15), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithValidRequestNotAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var key = new Key();
            mockWalletWrapper.Setup(m => m.BuildTransaction("myWallet", "Account 1", "test", key.PubKey.GetAddress(Network.Main).ToString(), new Money(150000), FeeType.High, 1))
                .Returns(("hexString", new uint256(15), new Money(150)));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest()
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
            Assert.Equal("hexString", model.Hex);
            Assert.Equal(new Money(150), model.Fee);
            Assert.Equal(new uint256(15), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest()
            {
                WalletName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void BuildTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            var key = new Key();
            mockWalletWrapper.Setup(m => m.BuildTransaction("myWallet", "Account 1", "test", key.PubKey.GetAddress(Network.Main).ToString(), new Money(150000), FeeType.High, 1))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest()
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
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue building transaction.", error.Message);
        }

        [Fact]
        public void SendTransactionSuccessfulReturnsOkResponse()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.SendTransaction(new uint256(15555).ToString()))
                .Returns(true);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.SendTransaction(new SendTransactionRequest()
            {
                Hex = new uint256(15555).ToString()
            });

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void SendTransactionFailedReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.SendTransaction(new uint256(15555).ToString()))
                .Returns(false);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.SendTransaction(new SendTransactionRequest()
            {
                Hex = new uint256(15555).ToString()
            });

            StatusCodeResult viewResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(400, viewResult.StatusCode);
        }

        [Fact]
        public void SendTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("Hex", "Hex required.");
            IActionResult result = controller.SendTransaction(new SendTransactionRequest()
            {
                Hex = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Hex required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void SendTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.SendTransaction(new uint256(15555).ToString()))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            
            IActionResult result = controller.SendTransaction(new SendTransactionRequest()
            {
                Hex = new uint256(15555).ToString()
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

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

            Directory.CreateDirectory(dir);
            File.Create(dir + "/wallet1.wallet.json");
            File.Create(dir + "/wallet2.wallet.json");

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletFileExtension()).Returns("wallet.json");

            var controller = new WalletController(walletManager.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);            
            
            IActionResult result = controller.ListWalletsFiles();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(new DirectoryInfo(dir).FullName, model.WalletsPath);
            Assert.Equal(2, model.WalletsFiles.Count());
            Assert.EndsWith("wallet1.wallet.json", model.WalletsFiles.ElementAt(0));
            Assert.EndsWith("wallet2.wallet.json", model.WalletsFiles.ElementAt(1));
        }

        [Fact]
        public void ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel()
        {
            string dir = AssureEmptyDir("TestData/WalletControllerTest/ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            IActionResult result = controller.ListWalletsFiles();

            JsonResult viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(new DirectoryInfo(dir).FullName, model.WalletsPath);
            Assert.Equal(0, model.WalletsFiles.Count());            
        }

        [Fact]
        public void ListWalletFilesWithExceptionReturnsBadRequest()
        {            
            var dataFolder = new DataFolder(new NodeSettings { DataDir = "" });

            var controller = new WalletController(new Mock<IWalletManager>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, dataFolder);

            IActionResult result = controller.ListWalletsFiles();

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);            
        }

        [Fact]
        public void CreateNewAccountWithValidModelReturnsAccountName()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Returns(new HdAccount() { Name = "Account 1" });

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel()
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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel()
            {
                WalletName = "myWallet",
                Password = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void CreateNewAccountWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel()
            {
                WalletName = "myWallet",
                Password = "test"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }

        [Fact]
        public void GetUnusedAddressWithValidModelReturnsUnusedAddress()
        {
            HdAddress address = CreateAddress();
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress("myWallet", "Account 1"))
                .Returns(address);

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel()
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

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            controller.ModelState.AddModelError("AccountName", "An account name is required.");

            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel()
            {
                WalletName = "myWallet",
                AccountName = ""
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("An account name is required.", error.Description);
            Assert.Equal("Formatting error", error.Message);
        }

        [Fact]
        public void GetUnusedAddressWithExceptionReturnsBadRequest()
        {
            var mockWalletWrapper = new Mock<IWalletManager>();
            mockWalletWrapper.Setup(m => m.GetUnusedAddress("myWallet", "Account 1"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(mockWalletWrapper.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, It.IsAny<DataFolder>());
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel()
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal(1, errorResponse.Errors.Count);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }


        private HdAccount CreateAccount(string name)
        {
            return new HdAccount()
            {
                Name = name,
                HdPath = "1/2/3/4/5",
            };
        }

        private static SpendingDetails CreateSpendingDetails(TransactionData changeTransaction, PaymentDetails paymentDetails)
        {
            var spendingDetails = new SpendingDetails()
            {
                TransactionId = changeTransaction.Id,
                CreationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3)),
                BlockHeight = changeTransaction.BlockHeight
            };

            spendingDetails.Payments.Add(paymentDetails);
            return spendingDetails;
        }

        private static PaymentDetails CreatePaymentDetails(Money amount, HdAddress destinationAddress)
        {
            return new PaymentDetails()
            {
                Amount = amount,
                DestinationAddress = destinationAddress.Address,
                DestinationScriptPubKey = destinationAddress.ScriptPubKey
            };
        }

        private TransactionData CreateTransaction(uint256 id, Money amount, int? blockHeight, SpendingDetails spendingDetails = null, DateTimeOffset? creationTime = null)
        {
            if (creationTime == null)
            {
                creationTime = new DateTimeOffset(new DateTime(2017, 6, 23, 1, 2, 3));
            }

            return new TransactionData()
            {
                Amount = amount,
                Id = id,
                CreationTime = creationTime.Value,
                BlockHeight = blockHeight,
                SpendingDetails = spendingDetails
            };
        }

        private static HdAddress CreateAddress(bool changeAddress = false)
        {
            var hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress()
            {
                Address = key.PubKey.GetAddress(Network.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }

        public ChainedBlock AppendBlock(ChainedBlock previous, params ConcurrentChain[] chains)
        {
            ChainedBlock last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                var block = new Block();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedBlock AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedBlock index = null;
            return AppendBlock(index, chains);
        }
    }
}
