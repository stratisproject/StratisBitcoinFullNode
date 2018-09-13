using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
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
            var loggerFactory = nodeSettings.LoggerFactory;

            this.walletManager = new WalletManager(loggerFactory, this.Network, new ConcurrentChain(this.Network),
                nodeSettings, walletSettings, dataFolder, new Mock<IWalletFeePolicy>().Object,
                new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            var walletTransactionHandler = new WalletTransactionHandler(loggerFactory, this.walletManager,
                new Mock<IWalletFeePolicy>().Object, this.Network, new StandardTransactionPolicy(this.Network));

            this.coldStakingManager = new ColdStakingManager(loggerFactory, this.walletManager, walletTransactionHandler,
                new Mock<IDateTimeProvider>().Object);

            this.coldStakingController = new ColdStakingController(loggerFactory, this.coldStakingManager);
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

            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, false, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName2, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName2, false, walletPassword);

            var wallet1 = this.walletManager.GetWalletByName(walletName1);
            var wallet2 = this.walletManager.GetWalletByName(walletName2);

            var coldAddress1 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName1, true);
            var hotAddress1 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName1, false);
            var coldAddress2 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName2, true);
            var hotAddress2 = this.coldStakingManager.GetFirstUnusedColdStakingAddress(walletName2, false);

            Assert.Equal(coldWalletAddress1, coldAddress1.Address.ToString());
            Assert.Equal(hotWalletAddress1, hotAddress1.Address.ToString());
            Assert.Equal(coldWalletAddress2, coldAddress2.Address.ToString());
            Assert.Equal(hotWalletAddress2, hotAddress2.Address.ToString());
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will fail if the wallet does not contain the relevant account.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForMissingAccountThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result1 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var errorResult1 = Assert.IsType<ErrorResult>(result1);
            var errorResponse1 = Assert.IsType<ErrorResponse>(errorResult1.Value);
            Assert.Single(errorResponse1.Errors);
            ErrorModel error1 = errorResponse1.Errors[0];

            IActionResult result2 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = false
            });

            var errorResult2 = Assert.IsType<ErrorResult>(result2);
            var errorResponse2 = Assert.IsType<ErrorResponse>(errorResult2.Value);
            Assert.Single(errorResponse2.Errors);
            ErrorModel error2 = errorResponse1.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error1.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error1.Description);
            Assert.StartsWith("The cold staking account does not exist.", error1.Message);

            Assert.Equal((int)HttpStatusCode.BadRequest, error2.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error2.Description);
            Assert.StartsWith("The cold staking account does not exist.", error2.Message);
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

            // Create existing accounts.
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, true, walletPassword);
            this.coldStakingManager.GetOrCreateColdStakingAccount(walletName1, false, walletPassword);

            // Try to get cold wallet address on existing account without supplying the wallet password.
            IActionResult result1 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = true
            });

            var jsonResult1 = Assert.IsType<JsonResult>(result1);
            var response1 = Assert.IsType<GetColdStakingAddressResponse>(jsonResult1.Value);

            // Try to get hot wallet address on existing account without supplying the wallet password.
            IActionResult result2 = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                IsColdWalletAddress = false
            });

            var jsonResult2 = Assert.IsType<JsonResult>(result2);
            var response2 = Assert.IsType<GetColdStakingAddressResponse>(jsonResult2.Value);

            Assert.Equal(coldWalletAddress1, response1.Address);
            Assert.Equal(hotWalletAddress1, response2.Address);
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
            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error.Description);
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
                HotWalletAddress = new Key().PubKey.GetAddress(this.Network).ToString(),
                ColdWalletAddress = new Key().PubKey.GetAddress(this.Network).ToString(),
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
            Assert.Equal((int)HttpStatusCode.BadRequest, error.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error.Description);
            Assert.StartsWith("The hot and cold wallet addresses could not be found in the corresponding accounts.", error.Message);
        }

        /// <summary>
        /// Confirms that a wallet exception will result from attempting to use coins from a cold
        /// staking account to act as inputs to a cold staking setup transaction.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithInvalidAccountThrowsWalletException()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);

            string coldWalletAccountName = typeof(ColdStakingManager).GetPrivateConstantValue<string>("ColdWalletAccountName");
            string hotWalletAccountName = typeof(ColdStakingManager).GetPrivateConstantValue<string>("HotWalletAccountName");

            // Attempt to set up cold staking with a cold wallet account name.
            IActionResult result1 = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = coldWalletAccountName,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult1 = Assert.IsType<ErrorResult>(result1);
            var errorResponse1 = Assert.IsType<ErrorResponse>(errorResult1.Value);
            Assert.Single(errorResponse1.Errors);
            ErrorModel error1 = errorResponse1.Errors[0];

            // Attempt to set up cold staking with a hot wallet account name.
            IActionResult result2 = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = hotWalletAccountName,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            var errorResult2 = Assert.IsType<ErrorResult>(result2);
            var errorResponse2 = Assert.IsType<ErrorResponse>(errorResult2.Value);
            Assert.Single(errorResponse2.Errors);
            ErrorModel error2 = errorResponse2.Errors[0];

            Assert.Equal((int)HttpStatusCode.BadRequest, error1.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error1.Description);
            Assert.StartsWith($"You can't perform this operation with wallet account '{coldWalletAccountName}'.", error1.Message);

            Assert.Equal((int)HttpStatusCode.BadRequest, error2.Status);
            Assert.StartsWith($"{nameof(Stratis)}.{nameof(Bitcoin)}.{nameof(Features)}.{nameof(Wallet)}.{nameof(WalletException)}", error2.Description);
            Assert.StartsWith($"You can't perform this operation with wallet account '{hotWalletAccountName}'.", error2.Message);
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
                WalletAccount = walletAccount,
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
                WalletAccount = walletAccount,
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

        /// <summary>
        /// Adds a spendable cold staking transaction to a wallet.
        /// </summary>
        /// <param name="wallet">Wallet to add the transaction to.</param>
        /// <returns>The spendable transaction that was added to the wallet.</returns>
        private Transaction AddSpendableColdstakingTransactionToWallet(Wallet.Wallet wallet)
        {
            // Get first unused cold staking address.
            this.coldStakingManager.GetOrCreateColdStakingAccount(wallet.Name, true, walletPassword);
            HdAddress address = this.coldStakingManager.GetFirstUnusedColdStakingAddress(wallet.Name, true);

            TxDestination hotPubKey = BitcoinAddress.Create(hotWalletAddress1, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);
            TxDestination coldPubKey = BitcoinAddress.Create(coldWalletAddress1, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);

            var scriptPubKey = new Script(OpcodeType.OP_DUP, OpcodeType.OP_HASH160, OpcodeType.OP_ROT, OpcodeType.OP_IF,
                OpcodeType.OP_CHECKCOLDSTAKEVERIFY, Op.GetPushOp(hotPubKey.ToBytes()), OpcodeType.OP_ELSE, Op.GetPushOp(coldPubKey.ToBytes()),
                OpcodeType.OP_ENDIF, OpcodeType.OP_EQUALVERIFY, OpcodeType.OP_CHECKSIG);

            var transaction = this.Network.CreateTransaction();

            transaction.Outputs.Add(new TxOut(Money.Coins(101), scriptPubKey));

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
                ScriptPubKey = scriptPubKey
            });

            return transaction;
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void ColdStakingWithdrawalWithColdWalletSucceeds()
        {
            this.Initialize();

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));
            this.walletManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);
            var wallet2 = this.walletManager.GetWalletByName(walletName2);

            Transaction prevTran = this.AddSpendableColdstakingTransactionToWallet(wallet2);

            BitcoinPubKeyAddress receivingAddress = new Key().PubKey.GetAddress(this.Network);

            IActionResult result = this.coldStakingController.ColdStakingWithdrawal(new ColdStakingWithdrawalRequest
            {
                ReceivingAddress = receivingAddress.ToString(),
                WalletName = walletName2,
                WalletPassword = walletPassword,
                Amount = "100",
                Fees = "0.01"
            });

            // TODO: Restore these lines.
            /*
            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<ColdStakingWithdrawalResponse>(jsonResult.Value);
            var transaction = Assert.IsType<PosTransaction>(this.Network.CreateTransaction(response.TransactionHex));
            Assert.Single(transaction.Inputs);
            Assert.Equal(prevTran.GetHash(), transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.Equal(Money.Coins(0.99m), transaction.Outputs[0].Value);
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY 90c582cb91d6b6d777c31c891d4943fed4edac3a OP_ELSE 0f8a4018d5aa05d2ec5b74acaa60db2234e6e735 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[0].ScriptPubKey.ToString());
            Assert.Equal(Money.Coins(100), transaction.Outputs[1].Value);
            Assert.Equal($"OP_DUP OP_HASH160 {receivingAddress.ScriptPubKey.Hash} OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);
            */
        }
    }
}
