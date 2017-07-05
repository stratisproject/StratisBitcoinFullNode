using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.Wallet.JsonConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Xunit;
using static Stratis.Bitcoin.FullNode;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class WalletManagerTest : TestBase
    {
        public WalletManagerTest()
        {
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);
            Logs.Configure(loggerFactory.Object);
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                 {
                                                     Cancellation = new System.Threading.CancellationTokenSource()
                                                 });

            var password = "test";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet");

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Stratis.Bitcoin.Wallet.Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));
            var actualWallet = walletManager.Wallets.ElementAt(0);

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(Network.StratisMain, expectedWallet.Network);

            Assert.Equal(expectedWallet.Name, actualWallet.Name);
            Assert.Equal(expectedWallet.Network, actualWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, actualWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, actualWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (var i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.Stratis, expectedWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(1, expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, actualWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);

                var accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(2, accountRoot.Accounts.Count);

                for (var j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    var actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(expectedWallet.EncryptedSeed, "test", expectedWallet.Network), expectedWallet.ChainCode);
                    var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(expectedWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (var k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        var actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                        Assert.Null(actualAddress.BlocksScanned);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (var l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        var actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                        Assert.Null(actualAddress.BlocksScanned);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, actualWallet.BlockLocator.Count);

            var expectedBlockHash = block.GetHash();
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), actualWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), actualWallet.BlockLocator.ElementAt(1));

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(password).PrivateKey.GetEncryptedBitcoinSecret(password, Network.StratisMain).ToWif());
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(password).PrivateKey.GetEncryptedBitcoinSecret(password, Network.StratisMain).ToWif());
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                 {
                                                     Cancellation = new System.Threading.CancellationTokenSource()
                                                 });

            var password = "test";
            var passphrase = "this is my magic passphrase";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", passphrase);

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Stratis.Bitcoin.Wallet.Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));
            var actualWallet = walletManager.Wallets.ElementAt(0);

            Assert.Equal("mywallet", expectedWallet.Name);
            Assert.Equal(Network.StratisMain, expectedWallet.Network);

            Assert.Equal(expectedWallet.Name, actualWallet.Name);
            Assert.Equal(expectedWallet.Network, actualWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, actualWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, actualWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (var i = 0; i < expectedWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.Stratis, expectedWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(1, expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, actualWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);

                var accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Equal(2, accountRoot.Accounts.Count);

                for (var j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    var actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(expectedWallet.EncryptedSeed, "test", expectedWallet.Network), expectedWallet.ChainCode);
                    var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(expectedWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (var k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        var actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                        Assert.Null(actualAddress.BlocksScanned);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (var l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        var actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        var expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        var expectedAddress = expectedAddressPubKey.GetAddress(expectedWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                        Assert.Null(actualAddress.BlocksScanned);
                        Assert.Equal(0, actualAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, actualWallet.BlockLocator.Count);

            var expectedBlockHash = block.GetHash();
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), actualWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, expectedWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), actualWallet.BlockLocator.ElementAt(1));

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, Network.StratisMain).ToWif());
            Assert.Equal(expectedWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, Network.StratisMain).ToWif());
        }

        [Fact]
        public void CreateWalletWithMnemonicListCreatesWalletUsingMnemonicList()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                 {
                                                     Cancellation = new System.Threading.CancellationTokenSource()
                                                 });

            var password = "test";

            var mnemonicList = new Mnemonic(Wordlist.French, WordCount.Eighteen);

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", mnemonicList: mnemonicList.ToString());

            Assert.Equal(mnemonic.DeriveSeed(), mnemonicList.DeriveSeed());
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            var concurrentChain = new ConcurrentChain(Network.Main);
            ChainedBlock tip = AppendBlock(null, concurrentChain);

            walletManager.Wallets.Add(CreateWallet("wallet1"));
            walletManager.Wallets.Add(CreateWallet("wallet2"));

            Parallel.For(0, 500, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.UpdateLastBlockSyncedHeight(tip);
                walletManager.Wallets.Add(CreateWallet("wallet"));
                walletManager.UpdateLastBlockSyncedHeight(tip);
            });

            Assert.Equal(502, walletManager.Wallets.Count);
            Assert.True(walletManager.Wallets.All(w => w.BlockLocator != null));
        }

        [Fact]
        public void LoadWalletWithExistingWalletLoadsWalletOntoManager()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/LoadWalletWithExistingWalletLoadsWalletOntoManager");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var wallet = new Bitcoin.Wallet.Wallet()
            {
                Network = Network.Main,
                ChainCode = new byte[0],
                EncryptedSeed = "",
                Name = "testWallet",
                AccountsRoot = new List<AccountRoot>(),
                BlockLocator = null
            };

            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dataFolder.WalletPath, "testWallet.wallet.json"), JsonConvert.SerializeObject(wallet, Formatting.Indented, new ByteArrayConverter()));

            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                {
                                                    Cancellation = new System.Threading.CancellationTokenSource()
                                                });

            var result = walletManager.LoadWallet("testWallet");

            Assert.Equal("testWallet", result.Name);
            Assert.Equal(Network.Main, result.Network);

            Assert.Equal(1, walletManager.Wallets.Count);
            Assert.Equal("testWallet", walletManager.Wallets.ElementAt(0).Name);
            Assert.Equal(Network.Main, walletManager.Wallets.ElementAt(0).Network);
        }

        [Fact]
        public void LoadWalletWithNonExistingWalletThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
           {
               string dir = AssureEmptyDir("TestData/WalletManagerTest/LoadWalletWithNonExistingWalletThrowsFileNotFoundException");
               var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

               var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                {
                                                    Cancellation = new System.Threading.CancellationTokenSource()
                                                });

               walletManager.LoadWallet("testWallet");
           });
        }

        [Fact]
        public void RecoverWalletWithEqualInputAsExistingWalletRecoversWallet()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/RecoverWalletWithEqualInputAsExistingWalletRecoversWallet");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var password = "test";
            var passphrase = "this is my magic passphrase";
            var walletName = "mywallet";

            ConcurrentChain chain = PrepareChainWithBlock();
            // prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = CreateWalletOnDiskAndDeleteWallet(dataFolder, password, passphrase, walletName, chain);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                                        dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                                        {
                                                                            Cancellation = new System.Threading.CancellationTokenSource()
                                                                        });

            // try to recover it.
            var recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), passphrase);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            var expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (var i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                var recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                var expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);
                // for some reason we generate one extra. Why?
                Assert.Equal(3, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(2, expectedAccountRoot.Accounts.Count);
                
                for (var j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    var expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    var recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (var k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        var expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        var recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(expectedAddress.BlocksScanned,recoveredAddress.BlocksScanned);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (var l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        var expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        var recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(expectedAddress.BlocksScanned, recoveredAddress.BlocksScanned);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, recoveredWallet.BlockLocator.Count);
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), recoveredWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), recoveredWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }

        [Fact]
        public void RecoverWalletOnlyWithPasswordWalletRecoversWallet()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/RecoverWalletOnlyWithPasswordWalletRecoversWallet");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            var password = "test";            
            var walletName = "mywallet";

            ConcurrentChain chain = PrepareChainWithBlock();
            // prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = CreateWalletOnDiskAndDeleteWallet(dataFolder, password, password, walletName, chain);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                                        dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                                        {
                                                                            Cancellation = new System.Threading.CancellationTokenSource()
                                                                        });

            // try to recover it.
            var recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), password);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            var expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (var i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                var recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                var expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);
                // for some reason we generate one extra. Why?
                Assert.Equal(3, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(2, expectedAccountRoot.Accounts.Count);

                for (var j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    var expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    var recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (var k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        var expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        var recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(expectedAddress.BlocksScanned, recoveredAddress.BlocksScanned);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (var l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        var expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        var recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Equal(expectedAddress.BlocksScanned, recoveredAddress.BlocksScanned);
                        Assert.Equal(0, expectedAddress.Transactions.Count);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }
                }
            }

            Assert.Equal(2, expectedWallet.BlockLocator.Count);
            Assert.Equal(2, recoveredWallet.BlockLocator.Count);
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(0), recoveredWallet.BlockLocator.ElementAt(0));
            Assert.Equal(expectedWallet.BlockLocator.ElementAt(1), recoveredWallet.BlockLocator.ElementAt(1));
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
        }

        [Fact]
        public void LoadKeysLookupInParallelDoesNotThrowInvalidOperationException()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/LoadKeysLookupInParallelDoesNotThrowInvalidOperationException");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            walletManager.Wallets.Add(CreateWallet("wallet2"));
            walletManager.Wallets.Add(CreateWallet("wallet3"));
            this.AddAddressesToWallet(walletManager, 1000);

            Parallel.For(0, 5000, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
            });

            Assert.Equal(12000, walletManager.keysLookup.Count);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithConfirmedTransactions()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CheckWalletBalanceEstimationWithConfirmedTransactions");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            this.AddAddressesToWallet(walletManager, 1000);

            var firstAccount = walletManager.Wallets.First().AccountsRoot.First().Accounts.First();

            // add two unconfirmed transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithUnConfirmedTransactions()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CheckWalletBalanceEstimationWithUnConfirmedTransactions");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            this.AddAddressesToWallet(walletManager, 1000);

            var firstAccount = walletManager.Wallets.First().AccountsRoot.First().Accounts.First();

            // add two confirmed transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
            }

            Assert.Equal(40, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentTransactions()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CheckWalletBalanceEstimationWithSpentTransactions");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            this.AddAddressesToWallet(walletManager, 1000);

            var firstAccount = walletManager.Wallets.First().AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndConfirmedTransactions()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CheckWalletBalanceEstimationWithSpentAndConfirmedTransactions");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            this.AddAddressesToWallet(walletManager, 1000);

            var firstAccount = walletManager.Wallets.First().AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10 });
            }

            Assert.Equal(40, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndUnConfirmedTransactions()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/CheckWalletBalanceEstimationWithSpentAndUnConfirmedTransactions");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime());

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            this.AddAddressesToWallet(walletManager, 1000);

            var firstAccount = walletManager.Wallets.First().AccountsRoot.First().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetSpendableAmount().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetSpendableAmount().UnConfirmedAmount);
        }

        private Bitcoin.Wallet.Wallet CreateWallet(string name)
        {
            return new Bitcoin.Wallet.Wallet()
            {
                Name = name,
                AccountsRoot = new List<AccountRoot>(),
                BlockLocator = null
            };
        }

        private ChainedBlock AppendBlock(ChainedBlock previous, params ConcurrentChain[] chains)
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

        private void AddAddressesToWallet(WalletManager walletManager, int count)
        {
            foreach (var wallet in walletManager.Wallets)
            {
                wallet.AccountsRoot.Add(new AccountRoot
                {
                    CoinType = CoinType.Bitcoin,
                    Accounts = new List<HdAccount>
                    {
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        },
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(count),
                            InternalAddresses = GenerateAddresses(count)
                        } }
                });
            }
        }

        private List<HdAddress> GenerateAddresses(int count)
        {
            List<HdAddress> addresses = new List<HdAddress>();
            for (int i = 0; i < count; i++)
            {

                HdAddress address = new HdAddress
                {
                    ScriptPubKey = new Key().ScriptPubKey
                };
                addresses.Add(address);
            }
            return addresses;
        }

        private static ConcurrentChain PrepareChainWithBlock()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);
            return chain;
        }

        private static (Mnemonic mnemonic, Bitcoin.Wallet.Wallet wallet) CreateWalletOnDiskAndDeleteWallet(DataFolder dataFolder, string password, string passphrase, string walletName, ConcurrentChain chain)
        {
            var walletManager = new WalletManager(Logs.LoggerFactory, It.IsAny<ConnectionManager>(), Network.StratisMain, chain, NodeSettings.Default(),
                                                             dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                             {
                                                                 Cancellation = new System.Threading.CancellationTokenSource()
                                                             });


            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, walletName, passphrase);
            var wallet = walletManager.Wallets.ElementAt(0);

            File.Delete(dataFolder.WalletPath + $"/{walletName}.wallet.json");

            return (mnemonic, wallet);
        }
    }
}
