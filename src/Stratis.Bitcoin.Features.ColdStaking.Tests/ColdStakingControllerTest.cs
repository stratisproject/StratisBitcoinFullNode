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
    /// This class tests the functionality provided by the <see cref="coldStakingController"/>.
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
        private const string hotWalletAddress2 = "Sagbh9LuzNAV7y2FHyUQJcgmjcuogSssef";

        private WalletManager walletManager;
        private ColdStakingManager coldStakingManager;
        private ColdStakingController coldStakingController;

        public ColdStakingControllerTest() : base(KnownNetworks.StratisMain)
        {
        }

        /// <summary>
        /// Initializes each test case.
        /// </summary>
        /// <param name="caller">An instance of the test class.</param>
        /// <param name="callingMethod">The test method being executed.</param>
        private void Initialize(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            var dataFolder = CreateDataFolder(caller);
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
        private Transaction AddSpendableTransactionToWallet(Wallet.Wallet wallet)
        {
            HdAddress address = wallet.GetAllAddressesByCoinType(CoinType.Stratis).FirstOrDefault();

            var transaction = this.Network.CreateTransaction();

            var amount = Money.Coins(101);

            transaction.Outputs.Add(
                new TxOut(
                    amount,
                    address.ScriptPubKey));

            address.Transactions.Add(new TransactionData()
            {
                Hex = transaction.ToHex(this.Network),
                Amount = amount,
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
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));
            this.walletManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            var wallet1 = this.walletManager.GetWalletByName(walletName1);
            var wallet2 = this.walletManager.GetWalletByName(walletName2);

            var coldAccount1 = this.coldStakingManager.GetColdStakingAccount(wallet1, true, walletPassword);
            var hotAccount1 = this.coldStakingManager.GetColdStakingAccount(wallet1, false, walletPassword);
            var coldAccount2 = this.coldStakingManager.GetColdStakingAccount(wallet2, true, walletPassword);
            var hotAccount2 = this.coldStakingManager.GetColdStakingAccount(wallet2, false, walletPassword);

            Assert.Equal(coldWalletAddress1, coldAccount1.ExternalAddresses.First().Address.ToString());
            Assert.Equal(hotWalletAddress1, hotAccount1.ExternalAddresses.First().Address.ToString());
            Assert.Equal(coldWalletAddress2, coldAccount2.ExternalAddresses.First().Address.ToString());
            Assert.Equal(hotWalletAddress2, coldAccount2.ExternalAddresses.First().Address.ToString());
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// wil fail if the wallet does not contain the relevant account and no password has been supplied.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForMissingAccountWithoutPasswordThrowsWalletException()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                WalletPassword = null,
                IsColdWalletAddress = true
            });

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("Stratis.Bitcoin.Features.Wallet.WalletException", error.Description);
            Assert.StartsWith("The address or associated account does not exist.", error.Message);
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will return an address even if the wallet does not contain the relevant account but a password has been supplied.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForMissingAccountWithPasswordReturnsAddress()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                WalletPassword = walletPassword,
                IsColdWalletAddress = false
            });

            var jsonResult = Assert.IsType<JsonResult>(result);
            var response = Assert.IsType<GetColdStakingAddressResponse>(jsonResult.Value);

            Assert.Equal(hotWalletAddress1, response.Address);
        }

        /// <summary>
        /// Confirms that <see cref="ColdStakingController.GetColdStakingAddress(GetColdStakingAddressRequest)"/>
        /// will return an address if the wallet contains the relevant account even if no password is supplied.
        /// </summary>
        [Fact]
        public void GetColdStakingAddressForExistingAccountWithoutPasswordReturnsAddress()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            // Create existing account.
            this.coldStakingManager.GetColdStakingAccount(this.walletManager.GetWalletByName(walletName1), true, walletPassword);

            // Try to get address on existing account without supplying the wallet password.
            IActionResult result = this.coldStakingController.GetColdStakingAddress(new GetColdStakingAddressRequest
            {
                WalletName = walletName1,
                WalletPassword = null,
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
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

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
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = "1XgtnEn92fCcJY6UXECmsqnbvWq7RkJiaM",
                ColdWalletAddress = "1X9rtnQHqZV7FZz2icuQpWCUAj5AdyW2ym",
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
        /// Confirms that a wallet exception will result from attempting to use an address from a cold staking account
        /// as a hot or cold wallet addresses.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithInvalidAccountThrowsWalletException()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            IActionResult result = this.coldStakingController.SetupColdStaking(new SetupColdStakingRequest
            {
                HotWalletAddress = hotWalletAddress1,
                ColdWalletAddress = coldWalletAddress2,
                WalletName = walletName1,
                WalletAccount = $"account { Wallet.Wallet.ColdStakingAccountIndex }",
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
            Assert.StartsWith("You can't perform this operation with wallet account 'account 100000000'.", error.Message);
        }

        /// <summary>
        /// Confirms that cold staking setup with the hot wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithHotWalletSucceeds()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName1, walletPassphrase, new Mnemonic(walletMnemonic1));

            Transaction prevTran = this.AddSpendableTransactionToWallet(this.walletManager.GetWalletByName(walletName1));

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
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY ba11d4970e64351c88bf00f10b6280d658785a94 OP_ELSE 6032478c6ac8caa056668ea7d065c32ae7e6da55 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);
        }

        /// <summary>
        /// Confirms that cold staking setup with the cold wallet will succeed if no issues (as per above test cases) are encountered.
        /// </summary>
        [Fact]
        public void SetupColdStakingWithColdWalletSucceeds()
        {
            this.Initialize(this);

            this.walletManager.CreateWallet(walletPassword, walletName2, walletPassphrase, new Mnemonic(walletMnemonic2));

            Transaction prevTran = this.AddSpendableTransactionToWallet(this.walletManager.GetWalletByName(walletName2));

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
            Assert.Equal("OP_DUP OP_HASH160 OP_ROT OP_IF OP_CHECKCOLDSTAKEVERIFY ba11d4970e64351c88bf00f10b6280d658785a94 OP_ELSE 6032478c6ac8caa056668ea7d065c32ae7e6da55 OP_ENDIF OP_EQUALVERIFY OP_CHECKSIG", transaction.Outputs[1].ScriptPubKey.ToString());
            Assert.False(transaction.IsCoinBase || transaction.IsCoinStake || transaction.IsColdCoinStake);
        }
    }
}
