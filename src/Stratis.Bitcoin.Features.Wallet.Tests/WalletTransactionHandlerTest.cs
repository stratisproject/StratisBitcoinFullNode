using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBreeze.Utils;
using FluentAssertions;
using Moq;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletTransactionHandlerTest : LogsTestBase
    {
        public readonly string CostlyOpReturnData;
        private readonly StandardTransactionPolicy standardTransactionPolicy;
        private readonly IScriptAddressReader scriptAddressReader;

        public WalletTransactionHandlerTest()
        {
            // adding this data to the transaction output should increase the fee
            // 83 is the max size for the OP_RETURN script => 80 is the max for the content of the script
            byte[] maxQuantityOfBytes = Enumerable.Range(0, 80).Select(Convert.ToByte).ToArray();
            this.CostlyOpReturnData = Encoding.UTF8.GetString(maxQuantityOfBytes);

            this.standardTransactionPolicy = new StandardTransactionPolicy(this.Network);
            this.scriptAddressReader = new ScriptAddressReader();
        }

        [Fact]
        public void BuildTransactionThrowsWalletExceptionWhenMoneyIsZero()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, new Mock<IWalletManager>().Object, new Mock<IWalletFeePolicy>().Object, this.Network, this.standardTransactionPolicy);

                Transaction result = walletTransactionHandler.BuildTransaction(CreateContext(this.Network, new WalletAccountReference(), "password", new Script(), Money.Zero, FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionNoSpendableTransactionsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                wallet.AccountsRoot.ElementAt(0).Accounts.Add(
                    new HdAccount
                    {
                        Name = "account1",
                        ExternalAddresses = new List<HdAddress>(),
                        InternalAddresses = new List<HdAddress>()
                    });

                BlockHeader blockHeader = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                var chain = new ConcurrentChain(this.Network, new ChainedHeader(blockHeader, blockHeader.GetHash(), 1));

                string dataDir = "TestData/WalletTransactionHandlerTest/BuildTransactionNoSpendableTransactionsThrowsWalletException";
                var nodeSettings = new NodeSettings(network: this.Network, args: new string[] { $"-datadir={dataDir}" });
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(nodeSettings),
                    new DataFolder(nodeSettings.DataDir), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, new Mock<IWalletFeePolicy>().Object, this.Network, new StandardTransactionPolicy(this.Network));

                walletManager.Wallets.Add(wallet);

                var walletReference = new WalletAccountReference
                {
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(this.Network, walletReference, "password", new Script(), new Money(500), FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionFeeTooLowDefaultsToMinimumFee()
        {
            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                .Returns(new FeeRate(0));

            Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { address },
                InternalAddresses = new List<HdAddress>
            {
                new HdAddress {
                    Index = 0,
                    HdPath = $"m/44'/0'/0'/1/0",
                    Address = changeKeys.Address.ToString(),
                    Pubkey = changeKeys.PubKey.ScriptPubKey,
                    ScriptPubKey = changeKeys.Address.ScriptPubKey,
                    Transactions = new List<TransactionData>()
                }
            }
            });

            string dataDir = "TestData/WalletTransactionHandlerTest/BuildTransactionFeeTooLowThrowsWalletException";
            var nodeSettings = new NodeSettings(network: this.Network, args: new string[] { $"-datadir={dataDir}" });
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(nodeSettings),
                new DataFolder(nodeSettings.DataDir), walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.Network, this.standardTransactionPolicy);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            TransactionBuildContext context = CreateContext(this.Network, walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            Transaction transaction = walletTransactionHandler.BuildTransaction(context);
            Assert.Equal(new Money(this.Network.MinTxFee, MoneyUnit.Satoshi), context.TransactionFee);
        }

        [Fact]
        public void BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            Transaction transactionResult = testContext.WalletTransactionHandler.BuildTransaction(context);

            Transaction result = this.Network.CreateTransaction(transactionResult.ToHex());
            (PubKey PubKey, BitcoinPubKeyAddress Address) expectedChangeAddressKeys = WalletTestsHelpers.GenerateAddressKeys(testContext.Wallet, testContext.AccountKeys.ExtPubKey, "1/0");

            Assert.Single(result.Inputs);
            Assert.Equal(testContext.AddressTransaction.Id, result.Inputs[0].PrevOut.Hash);

            Assert.Equal(2, result.Outputs.Count);
            TxOut output = result.Outputs[0];
            Assert.Equal((testContext.AddressTransaction.Amount - context.TransactionFee - 7500), output.Value);
            Assert.Equal(expectedChangeAddressKeys.Address.ScriptPubKey, output.ScriptPubKey);

            output = result.Outputs[1];
            Assert.Equal(7500, output.Value);
            Assert.Equal(testContext.DestinationKeys.PubKey.ScriptPubKey, output.ScriptPubKey);

            Assert.Equal(testContext.AddressTransaction.Amount - context.TransactionFee, result.TotalOut);
            Assert.NotNull(transactionResult.GetHash());
            Assert.Equal(result.GetHash(), transactionResult.GetHash());
        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Empty_Should_Not_Add_Extra_Output()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            string opReturnData = "";

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            Transaction transactionResult = testContext.WalletTransactionHandler.BuildTransaction(context);

            transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).Should()
                .BeEmpty("because opReturnData is empty");
        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Null_Should_Not_Add_Extra_Output()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            string opReturnData = null;

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            Transaction transactionResult = testContext.WalletTransactionHandler.BuildTransaction(context);

            transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).Should()
                .BeEmpty("because opReturnData is null");
        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Neither_Null_Nor_Empty_Should_Add_Extra_Output_With_Data()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            string opReturnData = "some extra transaction info";
            byte[] expectedBytes = Encoding.UTF8.GetBytes(opReturnData);

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);
            Transaction transactionResult = testContext.WalletTransactionHandler.BuildTransaction(context);

            IEnumerable<TxOut> unspendableOutputs = transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).ToList();
            unspendableOutputs.Count().Should().Be(1);
            unspendableOutputs.Single().Value.Should().Be(Money.Zero);

            IEnumerable<Op> ops = unspendableOutputs.Single().ScriptPubKey.ToOps();
            ops.Count().Should().Be(2);
            ops.First().Code.Should().Be(OpcodeType.OP_RETURN);
            ops.Last().PushData.Should().BeEquivalentTo(expectedBytes);
        }

        [Fact]
        public void BuildTransaction_When_OpReturnAmount_Is_Populated_Should_Add_Extra_Output_With_Data_And_Amount()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            string opReturnData = "some extra transaction info";
            byte[] expectedBytes = Encoding.UTF8.GetBytes(opReturnData);

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, opReturnData);

            context.OpReturnAmount = Money.Coins(0.0001m);

            Transaction transactionResult = testContext.WalletTransactionHandler.BuildTransaction(context);

            IEnumerable<TxOut> unspendableOutputs = transactionResult.Outputs.Where(o => o.ScriptPubKey.IsUnspendable).ToList();
            unspendableOutputs.Count().Should().Be(1);
            unspendableOutputs.Single().Value.Should().Be(Money.Coins(0.0001m));

            IEnumerable<Op> ops = unspendableOutputs.Single().ScriptPubKey.ToOps();
            ops.Count().Should().Be(2);
            ops.First().Code.Should().Be(OpcodeType.OP_RETURN);
            ops.Last().PushData.Should().BeEquivalentTo(expectedBytes);
        }

        [Fact]
        public void BuildTransaction_When_OpReturnData_Is_Too_Long_Should_Fail_With_Helpful_Message()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            byte[] eightyOneBytes = Encoding.UTF8.GetBytes(this.CostlyOpReturnData).Concat(Convert.ToByte(1));
            string tooLongOpReturnString = Encoding.UTF8.GetString(eightyOneBytes);

            TransactionBuildContext context = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, tooLongOpReturnString);
            new Action(() => testContext.WalletTransactionHandler.BuildTransaction(context))
                .Should().Throw<ArgumentOutOfRangeException>()
                .And.Message.Should().Contain(" maximum size of 83");

        }

        [Fact]
        public void FundTransaction_Given__a_wallet_has_enough_inputs__When__adding_inputs_to_an_existing_transaction__Then__the_transaction_is_funded_successfully()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys1 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys2 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/2");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys3 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/3");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            // wallet with 4 coinbase outputs of 50 = 200 Bitcoin
            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address, 4);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { address },
                InternalAddresses = new List<HdAddress>()
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));
            var overrideFeeRate = new FeeRate(20000);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.Network, this.standardTransactionPolicy);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            // create a trx with 3 outputs 50 + 50 + 50 = 150 BTC
            var context = new TransactionBuildContext(this.Network)
            {
                AccountReference = walletReference,
                MinConfirmations = 0,
                FeeType = FeeType.Low,
                WalletPassword = "password",
                Recipients = new[]
                {
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys1.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys2.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys3.PubKey.ScriptPubKey }
                }.ToList()
            };

            Transaction fundTransaction = walletTransactionHandler.BuildTransaction(context);
            Assert.Equal(4, fundTransaction.Inputs.Count); // 4 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // 3 outputs with change

            // remove the change output
            fundTransaction.Outputs.Remove(fundTransaction.Outputs.First(f => f.ScriptPubKey == context.ChangeAddress.ScriptPubKey));
            // remove 3 inputs they will be added back by fund transaction
            fundTransaction.Inputs.RemoveAt(3);
            fundTransaction.Inputs.RemoveAt(2);
            fundTransaction.Inputs.RemoveAt(1);
            Assert.Single(fundTransaction.Inputs); // 4 inputs

            Transaction fundTransactionClone = this.Network.CreateTransaction(fundTransaction.ToBytes());
            var fundContext = new TransactionBuildContext(this.Network)
            {
                AccountReference = walletReference,
                MinConfirmations = 0,
                FeeType = FeeType.Low,
                WalletPassword = "password",
                Recipients = new List<Recipient>()
            };

            fundContext.OverrideFeeRate = overrideFeeRate;
            walletTransactionHandler.FundTransaction(fundContext, fundTransaction);

            foreach (TxIn input in fundTransactionClone.Inputs) // all original inputs are still in the trx
                Assert.Contains(fundTransaction.Inputs, a => a.PrevOut == input.PrevOut);

            Assert.Equal(4, fundTransaction.Inputs.Count); // we expect 4 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // we expect 4 outputs
            Assert.Equal(new Money(200, MoneyUnit.BTC) - fundContext.TransactionFee, fundTransaction.TotalOut);

            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys1.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys2.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys3.PubKey.ScriptPubKey);
        }

        [Fact]
        public void Given_AnInvalidAccountIsUsed_When_GetMaximumSpendableAmountIsCalled_Then_AnExceptionIsThrown()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(this.Network);
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.Network, this.standardTransactionPolicy);

            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { WalletTestsHelpers.CreateAccount("account 1") }
            });
            walletManager.Wallets.Add(wallet);

            Exception ex = Assert.Throws<WalletException>(() => walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "noaccount"), FeeType.Low, true));
            Assert.NotNull(ex);
            Assert.NotNull(ex.Message);
            Assert.NotEqual(string.Empty, ex.Message);
            Assert.IsType<WalletException>(ex);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new ConcurrentChain(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.Network, this.standardTransactionPolicy);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), 1, new SpendingDetails()));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, new SpendingDetails()));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), 3, new SpendingDetails()));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 4, new SpendingDetails()));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalledForConfirmedTransactions_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new ConcurrentChain(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.Network, this.standardTransactionPolicy);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, false);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsTheSumOfUnconfirmedTxs()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new ConcurrentChain(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.Network, this.standardTransactionPolicy);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, new Key().ScriptPubKey));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null, null, null, new Key().ScriptPubKey));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, null, null, new Key().ScriptPubKey));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null, null, null, new Key().ScriptPubKey));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(new Money(165000), result.max + result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoTransactions_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new ConcurrentChain(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<WalletFeePolicy>(), this.Network, this.standardTransactionPolicy);
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        /// <summary>
        /// Tests the <see cref="WalletTransactionHandler.EstimateFee(TransactionBuildContext)"/> method by
        /// comparing it's fee calculation with the transaction fee computed for the same tx in the
        /// <see cref="WalletTransactionHandler.BuildTransaction(TransactionBuildContext)"/> method.
        /// </summary>
        [Fact]
        public void EstimateFeeWithLowFeeMatchesBuildTxLowFee()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            // Context to build requires password in order to sign transaction.
            TransactionBuildContext buildContext = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            testContext.WalletTransactionHandler.BuildTransaction(buildContext);

            // Context for estimate does not need password.
            TransactionBuildContext estimateContext = CreateContext(this.Network, testContext.WalletReference, null, testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            Money fee = testContext.WalletTransactionHandler.EstimateFee(estimateContext);

            Assert.Equal(fee, buildContext.TransactionFee);
        }

        /// <summary>
        /// Tests the <see cref="WalletTransactionHandler.EstimateFee(TransactionBuildContext)"/> method by
        /// comparing it's fee calculation with the transaction fee computed for the same tx in the
        /// <see cref="WalletTransactionHandler.BuildTransaction(TransactionBuildContext)"/> method.
        /// </summary>
        [Fact]
        public void EstimateFee_WithLowFee_Matches_BuildTransaction_WithLowFee_With_Long_OpReturnData_added()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            // Context to build requires password in order to sign transaction.
            TransactionBuildContext buildContext = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            testContext.WalletTransactionHandler.BuildTransaction(buildContext);

            // Context for estimate does not need password.
            TransactionBuildContext estimateContext = CreateContext(this.Network, testContext.WalletReference, null, testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            Money feeEstimate = testContext.WalletTransactionHandler.EstimateFee(estimateContext);

            feeEstimate.Should().Be(buildContext.TransactionFee);
        }

        /// <summary>
        /// Make sure that if you add data to the transaction in an OP_RETURN the estimated fee increases
        /// </summary>
        [Fact]
        public void EstimateFee_Without_OpReturnData_Should_Be_Less_Than_Estimate_Fee_With_Costly_OpReturnData()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            // Context with OpReturnData
            TransactionBuildContext estimateContextWithOpReturn = CreateContext(this.Network, testContext.WalletReference, null, testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            Money feeEstimateWithOpReturn = testContext.WalletTransactionHandler.EstimateFee(estimateContextWithOpReturn);

            // Context without OpReturnData
            TransactionBuildContext estimateContextWithoutOpReturn = CreateContext(this.Network, testContext.WalletReference, null, testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, null);
            Money feeEstimateWithoutOpReturn = testContext.WalletTransactionHandler.EstimateFee(estimateContextWithoutOpReturn);

            feeEstimateWithOpReturn.Should().NotBe(feeEstimateWithoutOpReturn);
            feeEstimateWithoutOpReturn.Satoshi.Should().BeLessThan(feeEstimateWithOpReturn.Satoshi);
        }

        /// <summary>
        /// Make sure that if you add data to the transaction in an OP_RETURN the actual fee increases
        /// </summary>
        [Fact]
        public void Actual_Fee_Without_OpReturnData_Should_Be_Less_Than_Actual_Fee_With_Costly_OpReturnData()
        {
            WalletTransactionHandlerTestContext testContext = SetupWallet();

            // Context with OpReturnData
            TransactionBuildContext contextWithOpReturn = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, this.CostlyOpReturnData);
            testContext.WalletTransactionHandler.BuildTransaction(contextWithOpReturn);

            // Context without OpReturnData
            TransactionBuildContext contextWithoutOpReturn = CreateContext(this.Network, testContext.WalletReference, "password", testContext.DestinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0, null);
            testContext.WalletTransactionHandler.BuildTransaction(contextWithoutOpReturn);

            contextWithoutOpReturn.TransactionFee.Should().NotBe(contextWithOpReturn.TransactionFee);
            contextWithoutOpReturn.TransactionFee.Satoshi.Should().BeLessThan(contextWithOpReturn.TransactionFee.Satoshi);
        }


        public static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations, string opReturnData = null)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                OpReturnData = opReturnData,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }

        private WalletTransactionHandlerTestContext SetupWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            Wallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);
            TransactionData addressTransaction = address.Transactions.First();

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { address },
                InternalAddresses = new List<HdAddress>()
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                .Returns(new FeeRate(20000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain,
                new WalletSettings(NodeSettings.Default(this.Network)), dataFolder,
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, this.scriptAddressReader);
            var walletTransactionHandler =
                new WalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, this.Network, this.standardTransactionPolicy);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            return new WalletTransactionHandlerTestContext
            {
                Wallet = wallet,
                AccountKeys = accountKeys,
                DestinationKeys = destinationKeys,
                AddressTransaction = addressTransaction,
                WalletTransactionHandler = walletTransactionHandler,
                WalletReference = walletReference
            };
        }
    }

    /// <summary>
    /// Data carrier class for objects required to test the <see cref="WalletTransactionHandler"/>.
    /// </summary>
    public class WalletTransactionHandlerTestContext
    {
        public Wallet Wallet { get; set; }

        public (ExtKey ExtKey, string ExtPubKey) AccountKeys { get; set; }

        public (PubKey PubKey, BitcoinPubKeyAddress Address) DestinationKeys { get; set; }

        public TransactionData AddressTransaction { get; set; }

        public WalletTransactionHandler WalletTransactionHandler { get; set; }

        public WalletAccountReference WalletReference { get; set; }
    }
}