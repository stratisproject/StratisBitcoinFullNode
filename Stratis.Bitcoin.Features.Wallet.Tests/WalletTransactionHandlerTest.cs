using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class WalletTransactionHandlerTest : LogsTestBase
    {

        [Fact]
        public void BuildTransactionThrowsWalletExceptionWhenMoneyIsZero()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, new Mock<ConcurrentChain>().Object, new Mock<IWalletManager>().Object, new Mock<IWalletFeePolicy>().Object, Network.Main);

                var result = walletTransactionHandler.BuildTransaction(CreateContext(new WalletAccountReference(), "password", new Script(), Money.Zero, FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionNoSpendableTransactionsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                wallet.AccountsRoot.ElementAt(0).Accounts.Add(
                    new HdAccount()
                    {
                        Name = "account1",
                        ExternalAddresses = new List<HdAddress>(),
                        InternalAddresses = new List<HdAddress>()
                    });

                var chain = new Mock<ConcurrentChain>();
                chain.Setup(c => c.Tip).Returns(new ChainedBlock(new BlockHeader(), 1));

                var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chain.Object, NodeSettings.Default(),
                       new DataFolder(new NodeSettings() { DataDir = "/TestData/WalletTransactionHandlerTest/BuildTransactionNoSpendableTransactionsThrowsWalletException" }), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chain.Object, walletManager, new Mock<IWalletFeePolicy>().Object, Network.Main);

                walletManager.Wallets.Add(wallet);
                

                var walletReference = new WalletAccountReference()
                {
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", new Script(), new Money(500), FeeType.Medium, 2));
            });
        }


        [Fact]
        public void BuildTransactionFeeTooLowThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletFeePolicy = new Mock<IWalletFeePolicy>();
                walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                    .Returns(new FeeRate(0));

                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
                var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
                var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
                var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

                var address = new HdAddress()
                {
                    Index = 0,
                    BlocksScanned = new SortedList<int, int>(),
                    HdPath = $"m/44'/0'/0'/0/0",
                    Address = spendingKeys.Address.ToString(),
                    Pubkey = spendingKeys.PubKey.ScriptPubKey,
                    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                    Transactions = new List<TransactionData>()
                };

                var chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
                WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);

                wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount()
                {
                    Index = 0,
                    Name = "account1",
                    HdPath = "m/44'/0'/0'",
                    ExtendedPubKey = accountKeys.ExtPubKey,
                    ExternalAddresses = new List<HdAddress>() { address },
                    InternalAddresses = new List<HdAddress>()
                {
                    new HdAddress() {
                        Index = 0,
                        BlocksScanned = new SortedList<int, int>(),
                        HdPath = $"m/44'/0'/0'/1/0",
                        Address = changeKeys.Address.ToString(),
                        Pubkey = changeKeys.PubKey.ScriptPubKey,
                        ScriptPubKey = changeKeys.Address.ScriptPubKey,
                        Transactions = new List<TransactionData>() {
                        }
                    }
                }
                });

                var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chain, NodeSettings.Default(),
                      new DataFolder(new NodeSettings() { DataDir = "/TestData/WalletTransactionHandlerTest/BuildTransactionFeeTooLowThrowsWalletException" }), walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chain, walletManager, walletFeePolicy.Object, Network.Main);

                walletManager.Wallets.Add(wallet);

                var walletReference = new WalletAccountReference()
                {
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0));
            });
        }

        [Fact]
        public void BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress()
        {
            var dataFolder = AssureEmptyDirAsDataFolder("TestData/WalletTransactionHandlerTest/BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress");

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");

            var address = new HdAddress()
            {
                Index = 0,
                BlocksScanned = new SortedList<int, int>(),
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address);
            var addressTransaction = address.Transactions.First();

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount()
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress>() { address },
                InternalAddresses = new List<HdAddress>()
                {
                    // no change addresses at the moment!
                }
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                .Returns(new FeeRate(20000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chain, NodeSettings.Default(), dataFolder, 
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chain, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference()
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            var result = new Transaction(transactionResult.ToHex());
            var expectedChangeAddressKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            Assert.Equal(1, result.Inputs.Count);
            Assert.Equal(addressTransaction.Id, result.Inputs[0].PrevOut.Hash);

            Assert.Equal(2, result.Outputs.Count);
            var output = result.Outputs[0];
            Assert.Equal((addressTransaction.Amount - context.TransactionFee - 7500), output.Value);
            Assert.Equal(expectedChangeAddressKeys.Address.ScriptPubKey, output.ScriptPubKey);

            output = result.Outputs[1];
            Assert.Equal(7500, output.Value);
            Assert.Equal(destinationKeys.PubKey.ScriptPubKey, output.ScriptPubKey);

            Assert.Equal(addressTransaction.Amount - context.TransactionFee, result.TotalOut);
            Assert.NotNull(transactionResult.GetHash());
            Assert.Equal(result.GetHash(), transactionResult.GetHash());
        }

        [Fact]
        public void FundTransaction_Given__a_wallet_has_enough_inputs__When__adding_inputs_to_an_existing_transaction__Then__the_transaction_is_funded_successfully()
        {
            var dataFolder = AssureEmptyDirAsDataFolder("TestData/WalletTransactionHandlerTest/FundTransaction_Given__a_wallet_has_enough_inputs__When__adding_inputs_to_an_existing_transaction__Then__the_transaction_is_funded_successfully");

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys1 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var destinationKeys2 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/2");
            var destinationKeys3 = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/3");


            var address = new HdAddress()
            {
                Index = 0,
                BlocksScanned = new SortedList<int, int>(),
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            // wallet with 4 coinbase outputs of 50 = 200 Bitcoin
            var chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, address, 4);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount()
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress>() { address },
                InternalAddresses = new List<HdAddress>()
                {
                    // no change addresses at the moment!
                }
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));
            var overrideFeeRate = new FeeRate(20000);

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chain, NodeSettings.Default(), dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chain, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference()
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            // create a trx with 3 outputs 50 + 50 + 49 = 149 BTC
            var context = new TransactionBuildContext(walletReference,
                new[] 
                {
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys1.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys2.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(49, MoneyUnit.BTC), ScriptPubKey = destinationKeys3.PubKey.ScriptPubKey }
                }
                .ToList(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };
            
            var fundTransaction = walletTransactionHandler.BuildTransaction(context);
            Assert.Equal(3, fundTransaction.Inputs.Count); // 3 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // 3 outputs with change

            // remove the change output
            fundTransaction.Outputs.Remove(fundTransaction.Outputs.First(f => f.ScriptPubKey == context.ChangeAddress.ScriptPubKey));
            // remove 2 inputs they will be added back by fund transaction
            fundTransaction.Inputs.RemoveAt(2);
            fundTransaction.Inputs.RemoveAt(1);
            Assert.Equal(1, fundTransaction.Inputs.Count); // 3 inputs

            var fundTransactionClone = fundTransaction.Clone();
            var fundContext = new TransactionBuildContext(walletReference, new List<Recipient>(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };

            fundContext.OverrideFeeRate = overrideFeeRate;
            walletTransactionHandler.FundTransaction(fundContext, fundTransaction);

            foreach (var input in fundTransactionClone.Inputs) // all original inputs are still in the trx
                Assert.True(fundTransaction.Inputs.Any(a => a.PrevOut == input.PrevOut));

            Assert.Equal(3, fundTransaction.Inputs.Count); // we expect 3 inputs 
            Assert.Equal(4, fundTransaction.Outputs.Count); // we expect 4 outputs 
            Assert.Equal(new Money(150, MoneyUnit.BTC) - fundContext.TransactionFee, fundTransaction.TotalOut);

            Assert.True(fundTransaction.Outputs.Any(a => a.ScriptPubKey == destinationKeys1.PubKey.ScriptPubKey));
            Assert.True(fundTransaction.Outputs.Any(a => a.ScriptPubKey == destinationKeys2.PubKey.ScriptPubKey));
            Assert.True(fundTransaction.Outputs.Any(a => a.ScriptPubKey == destinationKeys3.PubKey.ScriptPubKey));
        }

        [Fact]
        public void Given_AnInvalidAccountIsUsed_When_GetMaximumSpendableAmountIsCalled_Then_AnExceptionIsThrown()
        {
           string dir = AssureEmptyDir("TestData/WalletManagerTest/Given_AnInvalidAccountIsUsed_When_GetMaximumSpendableAmountIsCalled_Then_AnExceptionIsThrown");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, It.IsAny<ConcurrentChain>(), walletManager, It.IsAny<WalletFeePolicy>(), Network.Main);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot
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
            string dir = AssureEmptyDir("TestData/WalletManagerTest/Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoSpendableFound_Then_MaxAmountReturnsAsZero");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new ConcurrentChain(Network.Main.GetGenesis().Header), NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, It.IsAny<ConcurrentChain>(), walletManager, It.IsAny<WalletFeePolicy>(), Network.Main);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), 1, new SpendingDetails()));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, new SpendingDetails()));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), 3, new SpendingDetails()));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 4, new SpendingDetails()));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot
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
            string dir = AssureEmptyDir("TestData/WalletManagerTest/Given_GetMaximumSpendableAmountIsCalledForConfirmedTransactions_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsZero");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new ConcurrentChain(Network.Main.GetGenesis().Header), NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, It.IsAny<ConcurrentChain>(), walletManager, It.IsAny<WalletFeePolicy>(), Network.Main);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot
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
            string dir = AssureEmptyDir("TestData/WalletManagerTest/Given_GetMaximumSpendableAmountIsCalledForConfirmedTransactions_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsZero");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new ConcurrentChain(Network.Main.GetGenesis().Header), NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, It.IsAny<ConcurrentChain>(), walletManager, walletFeePolicy.Object, Network.Main);

            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, new Key().ScriptPubKey));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null, null, null, new Key().ScriptPubKey));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, null, null, new Key().ScriptPubKey));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null, null, null, new Key().ScriptPubKey));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot
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
            string dir = AssureEmptyDir("TestData/WalletManagerTest/Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoTransactions_Then_MaxAmountReturnsAsZero");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new ConcurrentChain(Network.Main.GetGenesis().Header), NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, It.IsAny<ConcurrentChain>(), walletManager, It.IsAny<WalletFeePolicy>(), Network.Main);
            HdAccount account = WalletTestsHelpers.CreateAccount("account 1");
            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot
            {
                Accounts = new List<HdAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }
        
        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}
