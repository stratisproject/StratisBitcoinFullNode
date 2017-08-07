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
                var wallet = GenerateBlankWallet("myWallet1", "password");
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
                       new DataFolder(new NodeSettings() { DataDir = "/TestData/WalletManagerTest" }), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
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

                var wallet = GenerateBlankWallet("myWallet1", "password");
                var accountKeys = GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
                var spendingKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
                var destinationKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
                var changeKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

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

                var chainInfo = CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, address);
                TransactionData addressTransaction = CreateTransactionDataFromFirstBlock(chainInfo);
                address.Transactions.Add(addressTransaction);

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

                var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chainInfo.chain, NodeSettings.Default(),
                      new DataFolder(new NodeSettings() { DataDir = "/TestData/WalletManagerTest" }), walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
                var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chainInfo.chain, walletManager, walletFeePolicy.Object, Network.Main);

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
            var dataFolder = AssureEmptyDirAsDataFolder("TestData/WalletManagerTest/BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress");

            var wallet = GenerateBlankWallet("myWallet1", "password");
            var accountKeys = GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");

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

            var chainInfo = CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, address);
            TransactionData addressTransaction = CreateTransactionDataFromFirstBlock(chainInfo);
            address.Transactions.Add(addressTransaction);

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

            var walletManager = new WalletManager(this.LoggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, chainInfo.chain, NodeSettings.Default(), dataFolder, 
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());
            var walletTransactionHandler = new WalletTransactionHandler(this.LoggerFactory.Object, chainInfo.chain, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

            var walletReference = new WalletAccountReference()
            {
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            var result = new Transaction(transactionResult.ToHex());
            var expectedChangeAddressKeys = GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

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
        
        private Features.Wallet.Wallet GenerateBlankWallet(string name, string password)
        {
            return GenerateBlankWalletWithExtKey(name, password).wallet;
        }

        private (Features.Wallet.Wallet wallet, ExtKey key) GenerateBlankWalletWithExtKey(string name, string password)
        {
            Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);

            Features.Wallet.Wallet walletFile = new Features.Wallet.Wallet
            {
                Name = name,
                EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main).ToWif(),
                ChainCode = extendedKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = Network.Main,
                AccountsRoot = new List<AccountRoot> { new AccountRoot { Accounts = new List<HdAccount>(), CoinType = (CoinType)Network.Main.Consensus.CoinType } },
            };

            return (walletFile, extendedKey);
        }
        
        private static (ExtKey ExtKey, string ExtPubKey) GenerateAccountKeys(Features.Wallet.Wallet wallet, string password, string keyPath)
        {
            var accountExtKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, password, wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = accountExtKey.Derive(new KeyPath(keyPath)).Neuter().ToString(wallet.Network);
            return (accountExtKey, accountExtendedPubKey);
        }

        private static (PubKey PubKey, BitcoinPubKeyAddress Address) GenerateAddressKeys(Features.Wallet.Wallet wallet, string accountExtendedPubKey, string keyPath)
        {
            var addressPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(new KeyPath(keyPath)).PubKey;
            var address = addressPubKey.GetAddress(wallet.Network);

            return (addressPubKey, address);
        }

        
        private static TransactionData CreateTransactionDataFromFirstBlock((ConcurrentChain chain, uint256 blockHash, Block block) chainInfo)
        {
            var transaction = chainInfo.block.Transactions[0];

            var addressTransaction = new TransactionData()
            {
                Amount = transaction.TotalOut,
                BlockHash = chainInfo.blockHash,
                BlockHeight = chainInfo.chain.GetBlock(chainInfo.blockHash).Height,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(chainInfo.block.Header.Time),
                Id = transaction.GetHash(),
                Index = 0,
                ScriptPubKey = transaction.Outputs[0].ScriptPubKey,
            };
            return addressTransaction;
        }

        public (ConcurrentChain chain, uint256 blockhash, Block block) CreateChainAndCreateFirstBlockWithPaymentToAddress(Network network, HdAddress address)
        {
            var chain = new ConcurrentChain(network.GetGenesis().Header);

            Block block = new Block();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
            block.Header.UpdateTime(DateTimeOffset.UtcNow, network, chain.Tip);

            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), address.ScriptPubKey));

            block.AddTransaction(coinbase);
            block.Header.Nonce = 0;
            block.UpdateMerkleRoot();
            block.Header.CacheHashes();

            chain.SetTip(block.Header);

            return (chain, block.GetHash(), block);
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
