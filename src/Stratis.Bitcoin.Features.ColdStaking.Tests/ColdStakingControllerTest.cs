using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.ColdStaking.Controllers;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.ColdStaking.Tests
{
    /// <summary>
    /// This class tests the functionality provided by the <see cref="ColdStakingController"/>.
    /// </summary>
    public class ColdStakingControllerTest : TestBase
    {
        private const string walletName1 = "wallet1";
        private const string walletName2 = "wallet2";
        private const string walletAccount = "account 0";
        private const string walletPassword = "test";
        private const string walletPassphrase = "passphrase";
        private const string walletMnemonic1 = "close vanish burden journey attract open soul romance beach surprise home produce";
        private const string walletMnemonic2 = "wish happy anchor lava path reject cinnamon absurd energy mammal cliff version";
        private const string coldWalletAddress1 = "SNiAnXM2WmbMhUij9cbit62sR8U9FjFJr3";
        private const string hotWalletAddress1 = "SaVUwmJSvRiofghrePxrBQGoke1pLfmfXN";
        private const string coldWalletAddress2 = "Sagbh9LuzNAV7y2FHyUQJcgmjcuogSssef";
        private const string hotWalletAddress2 = "SVoMim67CMF1St6j6toAWnnQ2mCvb8V4mT";

        private WalletManager walletManager;
        private ColdStakingManager coldStakingManager;
        private ColdStakingController coldStakingController;

        public ColdStakingControllerTest() : base(KnownNetworks.StratisMain)
        {
        }

        /// <summary>
        /// Initializes each test case.
        /// </summary>
        /// <param name="callingMethod">The test method being executed.</param>
        private void Initialize([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            var dataFolder = CreateDataFolder(this, callingMethod);
            var nodeSettings = new NodeSettings(this.Network, ProtocolVersion.ALT_PROTOCOL_VERSION);
            var walletSettings = new WalletSettings(nodeSettings);
            var loggerFactory = new Mock<LoggerFactory>();

            this.walletManager = new WalletManager(loggerFactory.Object, this.Network, new ConcurrentChain(this.Network),
                nodeSettings, walletSettings, dataFolder, new Mock<IWalletFeePolicy>().Object,
                new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            var walletTransactionHandler = new WalletTransactionHandler(loggerFactory.Object, this.walletManager,
                new Mock<IWalletFeePolicy>().Object, this.Network, new StandardTransactionPolicy(this.Network));
            this.coldStakingManager = new ColdStakingManager(loggerFactory.Object, this.walletManager, walletTransactionHandler,
                new Mock<IDateTimeProvider>().Object);

            this.coldStakingController = new ColdStakingController(loggerFactory.Object, this.coldStakingManager);
        }

        /// <summary>
        /// Adds a spendable transaction to a wallet.
        /// </summary>
        /// <param name="wallet">Wallet to add the transaction to.</param>
        /// <returns>The spendable transaction that was added to the wallet.</returns>
        private Transaction AddSpendableTransactionToWallet(Wallet.Wallet wallet)
        {
            HdAddress address = wallet.GetAllAddressesByCoinType(CoinType.Stratis).FirstOrDefault();

            var transaction = this.Network.CreateTransaction();

            transaction.Outputs.Add(new TxOut(Money.Coins(101), address.ScriptPubKey));

            address.Transactions.Add(new TransactionData()
            {
                Hex = transaction.ToHex(this.Network),
                Amount = transaction.Outputs[0].Value,
                Id = transaction.GetHash(),
                BlockHeight = 0,
                Index = 0,
                IsCoinBase = false,
                IsCoinStake = false,
                IsPropagated = true,
                BlockHash = this.Network.GenesisHash,
                ScriptPubKey = address.ScriptPubKey
            });

            return transaction;
        }

        /// <summary>
        /// Verifies that all the cold staking addresses are as expected. This allows us to use the
        /// previously established addresses instead of re-generating the addresses for each test case.
        /// </summary>
        [Fact]
        public void ColdStakingVerifyWalletAddresses()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));
            this.walletManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            this.coldStakingManager.CreateColdStakingAccount(walletName1, true, walletPassword);
            this.coldStakingManager.CreateColdStakingAccount(walletName1, false, walletPassword);
            this.coldStakingManager.CreateColdStakingAccount(walletName2, true, walletPassword);
            this.coldStakingManager.CreateColdStakingAccount(walletName2, false, walletPassword);

            var wallet1 = this.walletManager.GetWalletByName(walletName1);
            var wallet2 = this.walletManager.GetWalletByName(walletName2);

            var coldAddress1 = this.coldStakingManager.GetColdStakingAddress(walletName1, true);
            var hotAddress1 = this.coldStakingManager.GetColdStakingAddress(walletName1, false);
            var coldAddress2 = this.coldStakingManager.GetColdStakingAddress(walletName2, true);
            var hotAddress2 = this.coldStakingManager.GetColdStakingAddress(walletName2, false);

            Assert.Equal(coldWalletAddress1, coldAddress1.Address.ToString());
            Assert.Equal(hotWalletAddress1, hotAddress1.Address.ToString());
            Assert.Equal(coldWalletAddress2, coldAddress2.Address.ToString());
            Assert.Equal(hotWalletAddress2, hotAddress2.Address.ToString());
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// wil fail if the wallet does not contain the relevant account.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForMissingAccountThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("Stratis.Bitcoin.Features.Wallet.WalletException", error.Description);
            Assert.StartsWith("The cold staking account does not exist.", error.Message);
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will return an address if the wallet contains the relevant account.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForExistingAccountReturnsAddress()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            // Create existing account.
            this.coldStakingManager.CreateColdStakingAccount(walletName1, true, walletPassword);

            // Try to get address on existing account without supplying the wallet password.
            IActionResult result = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<GetColdStakingAddressResponse>(jsonResult.Value);

            Assert.Equal(coldWalletAddress1, response.Address);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use the same wallet
        /// as both cold wallet and hot wallet.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithSameWalletThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress1,
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("Stratis.Bitcoin.Features.Wallet.WalletException", error.Description);
            Assert.StartsWith("You can't use this wallet as both hot wallet and cold wallet.", error.Message);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use hot and cold wallet addresses
        /// where neither of the addresses is known to the wallet creating a cold staking setup.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithBothAddressesUnknownThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = "SXgbh9LuzNAV7y2FHyUQJcgmjcuogSssef",
                ColdWalletAddress = "SYgbh9LuzNAV7y2FHyUQJcgmjcuogSssef",
                WalletName = walletName1,
                WalletAccount = walletAccount,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("Stratis.Bitcoin.Features.Wallet.WalletException", error.Description);
            Assert.StartsWith("The hot and cold wallet addresses could not be found in the corresponding accounts.", error.Message);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to set up cold staking from a cold staking account.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithInvalidAccountThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = $"coldStakingColdAddresses",
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("Stratis.Bitcoin.Features.Wallet.WalletException", error.Description);
            Assert.StartsWith("You can't perform this operation with wallet account 'coldStakingColdAddresses'.", error.Message);
        }

        /// <summary>
        /// Confirms that cold staking setup with the hot wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithHotWalletSucceeds()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet1);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = $"account 0",
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 970e19fc2f6565b0b1c65fd88ef1512cb3da4d7b OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithColdWalletSucceeds()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet2 = this.walletManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableTransactionToWallet(wallet2);

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName2,
                WalletAccount = $"account 0",
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<SetupColdStakingResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 3d36028dc0fd3d3e433c801d9ebfff05ea663816 OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 92dfb829d31cefe6a0731f3432dea41596a278d9 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);
        }

        /// <summary>
        /// Cold staking info only confirms that a cold staking account exists once it has been created.
        /// </summary>
        [Fact]
        public void GetColdStakingInfoOnlyConfirmAccountExistenceOnceCreated()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result1 = this.coldStakingController.GetColdStakingInfo(new GetColdStakingInfoRequest
            {
                WalletName = walletName1
            });

            var jsonResult1 = Assert.IsType<JsonResult>(result1);
            var response1 = Assert.IsType<GetColdStakingInfoResponse>(jsonResult1.Value);

            Assert.False(response1.ColdWalletAccountExists);
            Assert.False(response1.HotWalletAccountExists);

            IActionResult result2 = this.coldStakingController.CreateColdStakingAccount(new CreateColdStakingAccountRequest
            {
                WalletName = walletName1,
                WalletPassword = walletPassword,
                IsColdWalletAccount = true
            });

            var jsonResult2 = Assert.IsType<JsonResult>(result2);
            var response2 = Assert.IsType<CreateColdStakingAccountResponse>(jsonResult2.Value);

            Assert.NotEmpty(response2.AccountName);

            IActionResult result3 = this.coldStakingController.GetColdStakingInfo(new GetColdStakingInfoRequest
            {
                WalletName = walletName1
            });

            var jsonResult3 = Assert.IsType<JsonResult>(result3);
            var response3 = Assert.IsType<GetColdStakingInfoResponse>(jsonResult3.Value);

            Assert.True(response3.ColdWalletAccountExists);
            Assert.False(response3.HotWalletAccountExists);
        }
    }
}
