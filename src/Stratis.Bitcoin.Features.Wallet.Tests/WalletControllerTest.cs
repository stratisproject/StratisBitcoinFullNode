using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletControllerTest : LogsTestBase
    {
        private readonly ConcurrentChain chain;

        public WalletControllerTest()
        {
            this.chain = new ConcurrentChain(this.Network);
        }

        [Fact]
        public void GenerateMnemonicWithoutParametersCreatesMnemonicWithDefaults()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic();

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithDifferentWordCountCreatesMnemonicWithCorrectNumberOfWords()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic(wordCount: 24);

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(24, resultingWords.Length);
        }

        [Fact]
        public void GenerateMnemonicWithStrangeLanguageCasingReturnsCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("eNgLiSh");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithEnglishWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("english");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.English.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithFrenchWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("french");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.French.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithSpanishWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("spanish");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.Spanish.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithJapaneseWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("japanese");

            var viewResult = Assert.IsType<JsonResult>(result);

            // japanese uses a JP space symbol.
            string[] resultingWords = (viewResult.Value as string).Split('　');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.Japanese.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseTraditionalWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesetraditional");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.ChineseTraditional.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithChineseSimplifiedWordListCreatesCorrectMnemonic()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("chinesesimplified");

            var viewResult = Assert.IsType<JsonResult>(result);

            string[] resultingWords = (viewResult.Value as string).Split(' ');

            Assert.Equal(12, resultingWords.Length);
            foreach (string word in resultingWords)
            {
                int index = -1;
                Assert.True(Wordlist.ChineseSimplified.WordExists(word, out index));
            }
        }

        [Fact]
        public void GenerateMnemonicWithUnknownLanguageReturnsBadRequest()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GenerateMnemonic("invalidlanguage");

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
            Assert.Equal("Invalid language 'invalidlanguage'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.", error.Message);
        }

        [Fact]
        public void CreateWalletSuccessfullyReturnsMnemonic()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Mnemonic>())).Returns(mnemonic);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                Password = "",
                Passphrase = "",
            });

            mockWalletCreate.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(mnemonic.ToString(), viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void CreateWalletWithInvalidModelStateReturnsBadRequest()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var mockWalletCreate = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Name cannot be empty.");

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "",
                Password = "",
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Name cannot be empty.", error.Message);
        }

        [Fact]
        public void CreateWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Mnemonic>()))
                .Throws(new WalletException(errorMessage));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                Password = "",
                Passphrase = "",
            });

            mockWalletCreate.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.Equal(errorMessage, error.Message);
        }

        [Fact]
        public void CreateWalletWithNotSupportedExceptionExceptionReturnsBadRequest()
        {
            var mockWalletCreate = new Mock<IWalletManager>();
            mockWalletCreate.Setup(wallet => wallet.CreateWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Mnemonic>()))
                .Throws(new NotSupportedException("Not supported"));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletCreate.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Create(new WalletCreationRequest
            {
                Name = "myName",
                Password = "",
                Passphrase = "",
            });

            mockWalletCreate.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("There was a problem creating a wallet.", error.Message);
        }

        [Fact]
        public void RecoverWalletSuccessfullyReturnsWalletModel()
        {
            var wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("mainnet")
            };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);

            Mock<IWalletSyncManager> walletSyncManager = new Mock<IWalletSyncManager>();
            walletSyncManager.Setup(w => w.WalletTip).Returns(new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GetGenesis().Header.GetHash(), 3));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic"
            });

            mockWalletManager.VerifyAll();
            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        /// <summary>
        /// This is to cover the scenario where a wallet is syncing at height X
        /// and the user recovers a new wallet at height X + Y.
        /// The wallet should continue syncing from X without jumpoing forward.
        /// </summary>
        [Fact]
        public void RecoverWalletWithDatedAfterCurrentSyncHeightDoesNotMoveSyncHeight()
        {
            var wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("mainnet")
            };

            // The chain is at height 100.
            ConcurrentChain concurrentChain = WalletTestsHelpers.GenerateChainWithHeight(100, this.Network);
            DateTime lastBlockDateTime = concurrentChain.Tip.Header.BlockTime.DateTime;

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null)).Returns(wallet);

            Mock<IWalletSyncManager> walletSyncManager = new Mock<IWalletSyncManager>();
            walletSyncManager.Setup(w => w.WalletTip).Returns(new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GetGenesis().Header.GetHash(), 3));
            walletSyncManager.Verify(w => w.SyncFromHeight(100), Times.Never);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic",
                CreationDate = lastBlockDateTime
            });

            mockWalletManager.VerifyAll();

            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void RecoverWalletWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void RecoverWalletWithInvalidOperationExceptionReturnsConflict()
        {
            string errorMessage = "An error occurred.";
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new WalletException(errorMessage));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic"
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(409, error.Status);
            Assert.Equal(errorMessage, error.Message);
        }

        [Fact]
        public void RecoverWalletWithFileNotFoundExceptionReturnsNotFound()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FileNotFoundException("File not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic"
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(404, error.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", error.Description);
            Assert.Equal("Wallet not found.", error.Message);
        }

        [Fact]
        public void RecoverWalletWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), null))
                .Throws(new FormatException("Formatting failed."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Recover(new WalletRecoveryRequest
            {
                Name = "myWallet",
                Password = "",
                Mnemonic = "mnemonic"
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
            Assert.Equal("Formatting failed.", error.Message);
        }

        [Fact]
        public void RecoverWalletViaExtPubKeySuccessfullyReturnsWalletModel()
        {
            string walletName = "myWallet";
            string extPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";

            this.RecoverWithExtPubAndCheckSuccessfulResponse(walletName, extPubKey);
        }

        [Fact]
        public void RecoverWalletViaExtPubKeySupportsStratisLegacyExtpubKey()
        {
            string walletName = "myWallet";
            string extPubKey = "xq5hcJV8uJDLaNytrg6FphHY1vdqxP1rCPhAmp4xZwpxzYyYEscYEujAmNR5NrPfy9vzQ6BajEqtFezcyRe4zcGHH3dR6BKaKov43JHd8UYhBVy";

            this.RecoverWithExtPubAndCheckSuccessfulResponse(walletName, extPubKey);
        }

        private void RecoverWithExtPubAndCheckSuccessfulResponse(string walletName, string extPubKey)
        {
            var wallet = new Wallet
            {
                Name = walletName,
                Network = KnownNetworks.StratisMain,
                IsExtPubKeyWallet = true
            };

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(w => w.RecoverWallet(walletName, It.IsAny<ExtPubKey>(), 1, It.IsAny<DateTime>())).Returns(wallet);

            Mock<IWalletSyncManager> walletSyncManager = new Mock<IWalletSyncManager>();
            walletSyncManager.Setup(w => w.WalletTip).Returns(new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GetGenesis().Header.GetHash(), 3));

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object,
                new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object,
                It.IsAny<ConnectionManager>(), KnownNetworks.StratisMain, this.chain,
                new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.RecoverViaExtPubKey(new WalletExtPubRecoveryRequest
            {
                Name = walletName,
                ExtPubKey = extPubKey,
                AccountIndex = 1,
            });

            walletManager.VerifyAll();

            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        /// <summary>
        /// This is to cover the scenario where a wallet is syncing at height X
        /// and the user recovers a new wallet at height X + Y.
        /// The wallet should continue syncing from X without jumpoing forward.
        /// </summary>
        [Fact]
        public void RecoverWalletWithExtPubDatedAfterCurrentSyncHeightDoesNotMoveSyncHeight()
        {
            string walletName = "myWallet";
            string extPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";

            var wallet = new Wallet
            {
                Name = walletName,
                Network = KnownNetworks.StratisMain,
                IsExtPubKeyWallet = true
            };

            // The chain is at height 100.
            ConcurrentChain concurrentChain = WalletTestsHelpers.GenerateChainWithHeight(100, this.Network);
            DateTime lastBlockDateTime = concurrentChain.Tip.Header.BlockTime.DateTime;

            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(w => w.RecoverWallet(It.IsAny<string>(), It.IsAny<ExtPubKey>(), 1, It.IsAny<DateTime>())).Returns(wallet);

            Mock<IWalletSyncManager> walletSyncManager = new Mock<IWalletSyncManager>();
            walletSyncManager.Setup(w => w.WalletTip).Returns(new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GetGenesis().Header.GetHash(), 3));
            walletSyncManager.Verify(w => w.SyncFromHeight(100), Times.Never);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object,
                new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object,
                It.IsAny<ConnectionManager>(), KnownNetworks.StratisMain, concurrentChain,
                new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.RecoverViaExtPubKey(new WalletExtPubRecoveryRequest
            {
                Name = walletName,
                ExtPubKey = extPubKey,
                AccountIndex = 1,
                CreationDate = lastBlockDateTime
            });

            walletManager.VerifyAll();

            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void LoadWalletSuccessfullyReturnsWalletModel()
        {
            var wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("mainnet")
            };
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                Password = ""
            });

            mockWalletManager.VerifyAll();
            var viewResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, viewResult.StatusCode);
        }

        [Fact]
        public void LoadWalletWithInvalidModelReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myWallet",
                Password = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void LoadWalletWithFileNotFoundExceptionandReturnsNotFound()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                Password = ""
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(404, error.Status);
            Assert.StartsWith("System.IO.FileNotFoundException", error.Description);
            Assert.Equal("This wallet was not found at the specified location.", error.Message);
        }

        [Fact]
        public void LoadWalletWithSecurityExceptionandReturnsForbidden()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<SecurityException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                Password = ""
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(403, error.Status);
            Assert.StartsWith("System.Security.SecurityException", error.Description);
            Assert.Equal("Wrong password, please try again.", error.Message);
        }

        [Fact]
        public void LoadWalletWithOtherExceptionandReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(wallet => wallet.LoadWallet(It.IsAny<string>(), It.IsAny<string>())).Throws<FormatException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.Load(new WalletLoadRequest
            {
                Name = "myName",
                Password = ""
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetGeneralInfoSuccessfullyReturnsWalletGeneralInfoModel()
        {
            var wallet = new Wallet
            {
                Name = "myWallet",
                Network = NetworkHelpers.GetNetwork("mainnet"),
                CreationTime = new DateTime(2017, 6, 19, 1, 1, 1),
                AccountsRoot = new List<AccountRoot> {
                    new AccountRoot()
                    {
                        CoinType = (CoinType)this.Network.Consensus.CoinType,
                        LastBlockSyncedHeight = 15
                    }
                }
            };

            var concurrentChain = new ConcurrentChain(this.Network);
            ChainedHeader tip = WalletTestsHelpers.AppendBlock(this.Network, null, new[] { concurrentChain });

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedPeers)
                .Returns(new NetworkPeerCollection());

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            string walletFileExtension = "wallet.json";
            string testWalletFileName = Path.ChangeExtension("myWallet", walletFileExtension);
            string testWalletPath = Path.Combine(AppContext.BaseDirectory, "stratisnode", testWalletFileName);
            string folder = Path.GetDirectoryName(testWalletPath);
            var files = new string[] { testWalletFileName };
            mockWalletManager.Setup(w => w.GetWalletsFiles()).Returns((folder, files));
            mockWalletManager.Setup(w => w.GetWalletFileExtension()).Returns(walletFileExtension);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, this.Network, concurrentChain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            mockWalletManager.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            var resultValue = Assert.IsType<WalletGeneralInfoModel>(viewResult.Value);

            Assert.Equal(wallet.Network, resultValue.Network);
            Assert.Equal(wallet.CreationTime, resultValue.CreationTime);
            Assert.Equal(15, resultValue.LastBlockSyncedHeight);
            Assert.Equal(0, resultValue.ConnectedNodes);
            Assert.Equal(tip.Height, resultValue.ChainTip);
            Assert.True(resultValue.IsDecrypted);
            Assert.Equal(testWalletPath, resultValue.WalletFilePath);
        }

        [Fact]
        public void GetGeneralInfoWithModelStateErrorReturnsBadRequest()
        {
            var wallet = new Wallet
            {
                Name = "myWallet",
            };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWallet("myWallet")).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Name", "Invalid name.");

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Invalid name.", error.Message);
        }

        [Fact]
        public void GetGeneralInfoWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWallet("myWallet")).Throws<FormatException>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.GetGeneralInfo(new WalletName
            {
                Name = "myWallet"
            });

            mockWalletManager.VerifyAll();
            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.FormatException", error.Description);
        }

        [Fact]
        public void GetHistoryWithoutAddressesReturnsEmptyModel()
        {
            string walletName = "myWallet";
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(new List<AccountHistory>
            {
                new AccountHistory
                {
                    History = new List<FlatHistory>(),
                    Account = new HdAccount()
                }
            });
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.NotNull(model.AccountsHistoryModel);
            Assert.NotEmpty(model.AccountsHistoryModel);
            Assert.Single(model.AccountsHistoryModel);
            Assert.Empty(model.AccountsHistoryModel.First().TransactionsHistory);
        }

        [Fact]
        public void GetHistoryWithValidModelWithoutTransactionSpendingDetailsReturnsWalletHistoryModel()
        {
            string walletName = "myWallet";
            HdAddress address = WalletTestsHelpers.CreateAddress();
            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            AccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Single(historyModel.TransactionsHistory);
            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

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
            string walletName = "myWallet";
            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(275000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address, changeAddress };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            AccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(2, historyModel.TransactionsHistory.Count);
            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);

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
            string walletName = "myWallet";

            HdAddress changeAddress = WalletTestsHelpers.CreateAddress(changeAddress: true);
            HdAddress address = WalletTestsHelpers.CreateAddress();
            HdAddress destinationAddress = WalletTestsHelpers.CreateAddress();

            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(310000), 1);
            changeAddress.Transactions.Add(changeTransaction);

            PaymentDetails paymentDetails = WalletTestsHelpers.CreatePaymentDetails(new Money(200000), destinationAddress);
            SpendingDetails spendingDetails = WalletTestsHelpers.CreateSpendingDetails(changeTransaction, paymentDetails);

            TransactionData transaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1, spendingDetails);
            address.Transactions.Add(transaction);

            var addresses = new List<HdAddress> { address, changeAddress };

            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            AccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(2, historyModel.TransactionsHistory.Count);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);
            Assert.Equal(0, resultingTransactionModel.Fee);
        }

        /// <summary>
        /// Tests that when a transaction has been sent that has multiple inputs to form the transaction these duplicate spending details do not show up multiple times in the history.
        /// </summary>
        [Fact]
        public void GetHistoryWithDuplicateSpentTransactionsSelectsDistinctsSpentTransactionsForDuplicates()
        {
            string walletName = "myWallet";
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
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            AccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Single(historyModel.TransactionsHistory);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

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
            string walletName = "myWallet";
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetHistory("myWallet", null)).Throws(new InvalidOperationException("Issue retrieving wallets."));
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(new Wallet());

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
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

            var addresses = new List<HdAddress> { address, changeAddress, changeAddress2 };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            var account = new HdAccount { ExternalAddresses = addresses };
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            List<FlatHistory> flat = addresses.SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToList();

            var mockWalletManager = new Mock<IWalletManager>();
            var accountsHistory = new List<AccountHistory> { new AccountHistory { History = flat, Account = account } };
            mockWalletManager.Setup(w => w.GetHistory(walletName, null)).Returns(accountsHistory);
            mockWalletManager.Setup(w => w.GetWalletByName(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetHistory(new WalletHistoryRequest
            {
                WalletName = walletName
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletHistoryModel;

            Assert.NotNull(model);
            Assert.Single(model.AccountsHistoryModel);

            AccountHistoryModel historyModel = model.AccountsHistoryModel.ElementAt(0);
            Assert.Equal(3, historyModel.TransactionsHistory.Count);

            TransactionItemModel resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(0);

            Assert.Equal(TransactionItemType.Received, resultingTransactionModel.Type);
            Assert.Equal(address.Address, resultingTransactionModel.ToAddress);
            Assert.Equal(transaction.Id, resultingTransactionModel.Id);
            Assert.Equal(transaction.Amount, resultingTransactionModel.Amount);
            Assert.Equal(transaction.CreationTime, resultingTransactionModel.Timestamp);
            Assert.Equal(transaction.BlockHeight, resultingTransactionModel.ConfirmedInBlock);
            Assert.Null(resultingTransactionModel.Fee);
            Assert.Equal(0, resultingTransactionModel.Payments.Count);

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(1);

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

            resultingTransactionModel = historyModel.TransactionsHistory.ElementAt(2);

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

            var accountsBalances = new List<AccountBalance>
            {
                new AccountBalance { Account = account, AmountConfirmed = new Money(130000), AmountUnconfirmed = new Money(35000), SpendableAmount = new Money(130000) },
                new AccountBalance { Account = account2, AmountConfirmed = new Money(108000), AmountUnconfirmed = new Money(139000), SpendableAmount = new Money(108000) }
            };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetBalances("myWallet", null)).Returns(accountsBalances);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(2, model.AccountsBalances.Count);

            AccountBalanceModel resultingBalance = model.AccountsBalances[0];
            Assert.Equal(this.Network.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account.Name, resultingBalance.Name);
            Assert.Equal(account.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);
            Assert.Equal(new Money(130000), resultingBalance.SpendableAmount);

            resultingBalance = model.AccountsBalances[1];
            Assert.Equal(this.Network.Consensus.CoinType, (int)resultingBalance.CoinType);
            Assert.Equal(account2.Name, resultingBalance.Name);
            Assert.Equal(account2.HdPath, resultingBalance.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
            Assert.Equal(new Money(108000), resultingBalance.SpendableAmount);
        }

        [Fact]
        public void GetBalanceWithEmptyListOfAccountsReturnsWalletBalanceModel()
        {
            var accounts = new List<HdAccount>();
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetAccounts("myWallet"))
                .Returns(accounts);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBalanceModel;

            Assert.NotNull(model);
            Assert.Empty(model.AccountsBalances);
        }

        [Fact]
        public void GetBalanceWithInvalidValidModelStateReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Message);
        }

        [Fact]
        public void GetBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetBalances("myWallet", null))
                  .Throws(new InvalidOperationException("Issue retrieving accounts."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetBalance(new WalletBalanceRequest
            {
                WalletName = "myWallet"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving accounts.", error.Message);
        }

        [Fact]
        public void GetAddressBalanceWithValidModelStateReturnsAddressBalanceModel()
        {
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress = WalletTestsHelpers.CreateAddress();
            account.InternalAddresses.Add(accountAddress);

            var addressBalance = new AddressBalance { Address = accountAddress.Address, AmountConfirmed = new Money(75000), AmountUnconfirmed = new Money(500000) };

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(w => w.GetAddressBalance(accountAddress.Address)).Returns(addressBalance);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = accountAddress.Address
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as AddressBalanceModel;

            Assert.NotNull(model);
            Assert.Equal(this.Network.Consensus.CoinType, (int)model.CoinType);
            Assert.Equal(accountAddress.Address, model.Address);
            Assert.Equal(new Money(75000), model.AmountConfirmed);
            Assert.Equal(new Money(500000), model.AmountUnconfirmed);
        }

        [Fact]
        public void GetAddressBalanceWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetAddressBalance("MyAddress"))
                  .Throws(new InvalidOperationException("Issue retrieving address balance."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = "MyAddress"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue retrieving address balance.", error.Message);
        }

        [Fact]
        public void GetAddressBalanceWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Address", "An address is required.");
            IActionResult result = controller.GetReceivedByAddress(new ReceivedByAddressRequest
            {
                Address = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("An address is required.", error.Message);
        }

        [Fact]
        public void BuildTransactionWithValidRequestAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithCustomFeeAmountAndFeeTypeReturnsWalletBuildTransactionModelWithFeeAmount()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeType = "105",
                FeeAmount = "0.1234",
                Password = "test",
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(12340000), model.Fee);
        }

        [Fact]
        public void BuildTransactionWithCustomFeeAmountAndNoFeeTypeReturnsWalletBuildTransactionModelWithFeeAmount()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = true,
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeAmount = "0.1234",
                Password = "test",
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(new Money(12340000), model.Fee);
        }

        [Fact]
        public void BuildTransactionWithValidRequestNotAllowingUnconfirmedReturnsWalletBuildTransactionModel()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var sentTrx = new Transaction();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Returns(sentTrx);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletBuildTransactionModel;

            Assert.NotNull(model);
            Assert.Equal(sentTrx.ToHex(), model.Hex);
            Assert.Equal(sentTrx.GetHash(), model.TransactionId);
        }

        [Fact]
        public void BuildTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A walletname is required.");
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                WalletName = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A walletname is required.", error.Message);
        }

        [Fact]
        public void BuildTransactionWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();

            var key = new Key();
            mockWalletTransactionHandler.Setup(m => m.BuildTransaction(It.IsAny<TransactionBuildContext>()))
                .Throws(new InvalidOperationException("Issue building transaction."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.BuildTransaction(new BuildTransactionRequest
            {
                AccountName = "Account 1",
                AllowUnconfirmed = false,
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeType = "105",
                Password = "test",
                WalletName = "myWallet"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.Equal("Issue building transaction.", error.Message);
        }

        [Fact]
        public void SendTransactionSuccessfulReturnsWalletSendTransactionModelResponse()
        {
            string transactionHex = "010000000189c041f79aac3aa7e7a72804a9a55cd9eceba41a0586640f602eb9823540ce89010000006b483045022100ab9597b37cb8796aefa30b207abb248c8003d4d153076997e375b0daf4f9f7050220546397fee1cefe54c49210ea653e9e61fb88adf51b68d2c04ad6d2b46ddf97a30121035cc9de1f233469dad8a3bbd1e61b699a7dd8e0d8370c6f3b1f2a16167da83546ffffffff02f6400a00000000001976a914accf603142aaa5e22dc82500d3e187caf712f11588ac3cf61700000000001976a91467872601dda216fbf4cab7891a03ebace87d8e7488ac00000000";

            var mockBroadcasterManager = new Mock<IBroadcasterManager>();

            mockBroadcasterManager.Setup(m => m.GetTransaction(It.IsAny<uint256>())).Returns(new TransactionBroadcastEntry(this.Network.CreateTransaction(transactionHex), State.Broadcasted, string.Empty));

            var connectionManagerMock = new Mock<IConnectionManager>();
            var peers = new List<INetworkPeer>();
            peers.Add(null);
            connectionManagerMock.Setup(c => c.ConnectedPeers).Returns(new TestReadOnlyNetworkPeerCollection(peers));

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object,
                new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, this.Network, this.chain, mockBroadcasterManager.Object, DateTimeProvider.Default);
            IActionResult result = controller.SendTransaction(new SendTransactionRequest(transactionHex));

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletSendTransactionModel;
            Assert.NotNull(model);
            Assert.Equal(new uint256("96b4f0c2f0aa2cecd43fa66b5e3227c56afd8791e18fcc572d9625ee05d6741c"), model.TransactionId);
            Assert.Equal("1GkjeiT7Y6RdPPL3p3nUME9DLJchhLNCsJ", model.Outputs.First().Address);
            Assert.Equal(new Money(671990), model.Outputs.First().Amount);
            Assert.Equal("1ASQW3EkkQ1zCpq3HAVfrGyVrSwVz4cbzU", model.Outputs.ElementAt(1).Address);
            Assert.Equal(new Money(1570364), model.Outputs.ElementAt(1).Amount);
        }

        [Fact]
        public void SendTransactionFailedBecauseNoNodesConnected()
        {
            var mockBroadcasterManager = new Mock<IBroadcasterManager>();

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.Setup(c => c.ConnectedPeers)
                .Returns(new NetworkPeerCollection());

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object,
                new Mock<IWalletSyncManager>().Object, connectionManagerMock.Object, this.Network, this.chain, mockBroadcasterManager.Object, DateTimeProvider.Default);

            IActionResult result = controller.SendTransaction(new SendTransactionRequest(new uint256(15555).ToString()));

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(403, error.Status);
            Assert.Equal("Can't send transaction: sending transaction requires at least one connection!", error.Message);
        }

        [Fact]
        public void SendTransactionWithInvalidModelStateReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Hex", "Hex required.");
            IActionResult result = controller.SendTransaction(new SendTransactionRequest(""));

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("Hex required.", error.Message);
        }

        [Fact]
        public void ListWalletFilesWithExistingWalletFilesReturnsWalletFileModel()
        {
            string walletPath = "walletPath";
            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Returns((walletPath, new[] { "wallet1.wallet.json", "wallet2.wallet.json" }));

            walletManager.Setup(m => m.GetWalletFileExtension()).Returns("wallet.json");

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(walletPath, model.WalletsPath);
            Assert.Equal(2, model.WalletsFiles.Count());
            Assert.EndsWith("wallet1.wallet.json", model.WalletsFiles.ElementAt(0));
            Assert.EndsWith("wallet2.wallet.json", model.WalletsFiles.ElementAt(1));
        }

        [Fact]
        public void ListWalletFilesWithoutExistingWalletFilesReturnsWalletFileModel()
        {
            string walletPath = "walletPath";
            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Returns((walletPath, Enumerable.Empty<string>()));

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as WalletFileModel;

            Assert.NotNull(model);
            Assert.Equal(walletPath, model.WalletsPath);
            Assert.Empty(model.WalletsFiles);
        }

        [Fact]
        public void ListWalletFilesWithExceptionReturnsBadRequest()
        {
            var walletManager = new Mock<IWalletManager>();
            walletManager.Setup(m => m.GetWalletsFiles())
                .Throws(new Exception("something happened."));

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);

            IActionResult result = controller.ListWalletsFiles();

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("something happened.", error.Message);
        }

        [Fact]
        public void CreateNewAccountWithValidModelReturnsAccountName()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Returns(new HdAccount { Name = "Account 1" });

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("Account 1", viewResult.Value as string);
        }

        [Fact]
        public void CreateNewAccountWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Password", "A password is required.");

            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A password is required.", error.Message);
        }

        [Fact]
        public void CreateNewAccountWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetUnusedAccount("myWallet", "test"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.CreateNewAccount(new GetUnusedAccountModel
            {
                WalletName = "myWallet",
                Password = "test"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }

        [Fact]
        public void ListAccountsWithValidModelStateReturnsAccounts()
        {
            string walletName = "wallet 1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);

            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount>()
                {
                    new HdAccount
                    {
                        Name = "account 0"
                    },
                    new HdAccount
                    {
                        Name = "account 1"
                    }
                }
            });

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetAccounts(walletName)).Returns(wallet.AccountsRoot.SelectMany(x => x.Accounts));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = "wallet 1"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<string>;

            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.Equal("account 0", model.First());
            Assert.Equal("account 1", model.Last());
        }

        [Fact]
        public void ListAccountsWithInvalidModelReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("WalletName", "A wallet name is required.");

            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("A wallet name is required.", error.Message);
        }

        [Fact]
        public void ListAccountsWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetAccounts("wallet 0"))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.ListAccounts(new ListAccountsModel
            {
                WalletName = "wallet 0",
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
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
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Returns(address);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(address.Address, viewResult.Value as string);
        }

        [Fact]
        public void GetUnusedAddressWithInvalidValidModelReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("AccountName", "An account name is required.");

            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = ""
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.Equal("An account name is required.", error.Message);
        }

        [Fact]
        public void GetUnusedAddressWithExceptionReturnsBadRequest()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetUnusedAddress(new WalletAccountReference("myWallet", "Account 1")))
                .Throws(new InvalidOperationException("Wallet not found."));

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetUnusedAddress(new GetUnusedAddressModel
            {
                WalletName = "myWallet",
                AccountName = "Account 1"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.InvalidOperationException", error.Description);
            Assert.StartsWith("Wallet not found.", error.Message);
        }

        [Fact]
        public void GetAllAddressesWithValidModelReturnsAllAddresses()
        {
            string walletName = "myWallet";

            // Receive address with a transaction
            HdAddress usedReceiveAddress = WalletTestsHelpers.CreateAddress();
            TransactionData receiveTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            usedReceiveAddress.Transactions.Add(receiveTransaction);

            // Receive address without a transaction
            HdAddress unusedReceiveAddress = WalletTestsHelpers.CreateAddress();

            // Change address with a transaction
            HdAddress usedChangeAddress = WalletTestsHelpers.CreateAddress(true);
            TransactionData changeTransaction = WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(500000), 1);
            usedChangeAddress.Transactions.Add(changeTransaction);

            // Change address without a transaction
            HdAddress unusedChangeAddress = WalletTestsHelpers.CreateAddress(true);

            var receiveAddresses = new List<HdAddress> { usedReceiveAddress, unusedReceiveAddress };
            var changeAddresses = new List<HdAddress> { usedChangeAddress, unusedChangeAddress };
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { new HdAccount
                {
                    ExternalAddresses = receiveAddresses,
                    InternalAddresses = changeAddresses,
                    Name = "Account 0"
                } }
            });

            var mockWalletManager = new Mock<IWalletManager>();
            mockWalletManager.Setup(m => m.GetWallet(walletName)).Returns(wallet);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetAllAddresses(new GetAllAddressesModel { WalletName = "myWallet", AccountName = "Account 0" });

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as AddressesModel;

            Assert.NotNull(model);
            Assert.Equal(4, model.Addresses.Count());

            AddressModel modelUsedReceiveAddress = model.Addresses.Single(a => a.Address == usedReceiveAddress.Address);
            Assert.Equal(modelUsedReceiveAddress.Address, model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).Address);
            Assert.False(model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).IsChange);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedReceiveAddress.Address).IsUsed);

            AddressModel modelUnusedReceiveAddress = model.Addresses.Single(a => a.Address == unusedReceiveAddress.Address);
            Assert.Equal(modelUnusedReceiveAddress.Address, model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).Address);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).IsChange);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedReceiveAddress.Address).IsUsed);

            AddressModel modelUsedChangeAddress = model.Addresses.Single(a => a.Address == usedChangeAddress.Address);
            Assert.Equal(modelUsedChangeAddress.Address, model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).Address);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).IsChange);
            Assert.True(model.Addresses.Single(a => a.Address == modelUsedChangeAddress.Address).IsUsed);

            AddressModel modelUnusedChangeAddress = model.Addresses.Single(a => a.Address == unusedChangeAddress.Address);
            Assert.Equal(modelUnusedChangeAddress.Address, model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).Address);
            Assert.True(model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).IsChange);
            Assert.False(model.Addresses.Single(a => a.Address == modelUnusedChangeAddress.Address).IsUsed);
        }

        [Fact]
        public void GetMaximumBalanceWithValidModelStateReturnsMaximumBalance()
        {
            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletTransactionHandler>().Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            controller.ModelState.AddModelError("Error in model", "There was an error in the model.");
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
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

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            var viewResult = Assert.IsType<JsonResult>(result);
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

            var controller = new WalletController(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetMaximumSpendableBalance(new WalletMaximumBalanceRequest
            {
                WalletName = "myWallet",
                AccountName = "account 1",
                FeeType = "low",
                AllowUnconfirmed = true
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            Assert.NotNull(errorResult.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, errorResult.StatusCode.Value);
        }

        [Fact]
        public void GetTransactionFeeEstimateWithValidRequestReturnsFee()
        {
            var mockWalletManager = new Mock<IWalletManager>();
            var mockWalletTransactionHandler = new Mock<IWalletTransactionHandler>();
            var key = new Key();
            var expectedFee = new Money(1000);
            mockWalletTransactionHandler.Setup(m => m.EstimateFee(It.IsAny<TransactionBuildContext>()))
                .Returns(expectedFee);

            var controller = new WalletController(this.LoggerFactory.Object, mockWalletManager.Object, mockWalletTransactionHandler.Object, new Mock<IWalletSyncManager>().Object, It.IsAny<ConnectionManager>(), this.Network, this.chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            IActionResult result = controller.GetTransactionFeeEstimate(new TxFeeEstimateRequest
            {
                AccountName = "Account 1",
                Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = key.PubKey.GetAddress(this.Network).ToString(), Amount = new Money(150000).ToString() } },
                FeeType = "105",
                WalletName = "myWallet"
            });

            var viewResult = Assert.IsType<JsonResult>(result);
            var actualFee = viewResult.Value as Money;

            Assert.NotNull(actualFee);
            Assert.Equal(expectedFee, actualFee);
        }

        [Fact]
        public void RemoveAllTransactionsWithSyncEnabledSyncsAfterRemoval()
        {
            // Arrange.
            string walletName = "wallet1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot());
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            uint256 trxId2 = uint256.Parse("a3dd63ec364fcb59043a1cf209285d8e60f1cc06186d4dcbce496cdbb4303401");
            var resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));
            resultModel.Add((trxId2, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveAllTransactions(walletName)).Returns(resultModel);
            walletManager.Setup(manager => manager.GetWallet(walletName)).Returns(wallet);
            walletSyncManager.Setup(manager => manager.SyncFromHeight(It.IsAny<int>()));
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            var requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = true,
                DeleteAll = true
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Once);

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId2) != null);
        }

        [Fact]
        public void RemoveAllTransactionsWithSyncDisabledDoesNotSyncAfterRemoval()
        {
            // Arrange.
            string walletName = "wallet1";
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            uint256 trxId2 = uint256.Parse("a3dd63ec364fcb59043a1cf209285d8e60f1cc06186d4dcbce496cdbb4303401");
            var resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));
            resultModel.Add((trxId2, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveAllTransactions(walletName)).Returns(resultModel);
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            var requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = false,
                DeleteAll = true
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Never);

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Equal(2, model.Count());
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId2) != null);
        }

        [Fact]
        public void RemoveTransactionsWithIdsRemovesAllTransactionsByIds()
        {
            // Arrange.
            string walletName = "wallet1";
            Wallet wallet = WalletTestsHelpers.CreateWallet(walletName);
            wallet.AccountsRoot.Add(new AccountRoot());
            uint256 trxId1 = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            var resultModel = new HashSet<(uint256 trxId, DateTimeOffset creationTime)>();
            resultModel.Add((trxId1, DateTimeOffset.Now));

            var walletManager = new Mock<IWalletManager>();
            var walletSyncManager = new Mock<IWalletSyncManager>();
            walletManager.Setup(manager => manager.RemoveTransactionsByIdsLocked(walletName, new[] { trxId1 })).Returns(resultModel);
            walletManager.Setup(manager => manager.GetWallet(walletName)).Returns(wallet);
            walletSyncManager.Setup(manager => manager.SyncFromHeight(It.IsAny<int>()));
            ConcurrentChain chain = WalletTestsHelpers.GenerateChainWithHeight(3, this.Network);

            var controller = new WalletController(this.LoggerFactory.Object, walletManager.Object, new Mock<IWalletTransactionHandler>().Object, walletSyncManager.Object, It.IsAny<ConnectionManager>(), this.Network, chain, new Mock<IBroadcasterManager>().Object, DateTimeProvider.Default);
            var requestModel = new RemoveTransactionsModel
            {
                WalletName = walletName,
                ReSync = true,
                TransactionsIds = new[] { "d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400" }
            };

            // Act.
            IActionResult result = controller.RemoveTransactions(requestModel);

            // Assert.
            walletManager.VerifyAll();
            walletManager.Verify(manager => manager.RemoveAllTransactions(It.IsAny<string>()), Times.Never);
            walletSyncManager.Verify(manager => manager.SyncFromHeight(It.IsAny<int>()), Times.Once);

            var viewResult = Assert.IsType<JsonResult>(result);
            var model = viewResult.Value as IEnumerable<RemovedTransactionModel>;
            Assert.NotNull(model);
            Assert.Single(model);
            Assert.True(model.SingleOrDefault(t => t.TransactionId == trxId1) != null);
        }
    }

    public class TestReadOnlyNetworkPeerCollection : IReadOnlyNetworkPeerCollection
    {
        public event EventHandler<NetworkPeerEventArgs> Added;
        public event EventHandler<NetworkPeerEventArgs> Removed;

        private List<INetworkPeer> networkPeers;

        public TestReadOnlyNetworkPeerCollection()
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = new List<INetworkPeer>();
        }

        public TestReadOnlyNetworkPeerCollection(List<INetworkPeer> peers)
        {
            this.Added = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.Removed = new EventHandler<NetworkPeerEventArgs>((obj, eventArgs) => { });
            this.networkPeers = peers;
        }

        public INetworkPeer FindByEndpoint(IPEndPoint endpoint)
        {
            return null;
        }

        public List<INetworkPeer> FindByIp(IPAddress ip)
        {
            return null;
        }

        public INetworkPeer FindLocal()
        {
            return null;
        }

        public IEnumerator<INetworkPeer> GetEnumerator()
        {
            return this.networkPeers.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}