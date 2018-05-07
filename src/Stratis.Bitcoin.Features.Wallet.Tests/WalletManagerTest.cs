using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly WalletFixture walletFixture;

        public WalletManagerTest(WalletFixture walletFixture)
        {
            this.walletFixture = walletFixture;
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet");

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));
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
                Assert.Equal(1, accountRoot.Accounts.Count);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";
            var passphrase = "this is my magic passphrase";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", passphrase);

            // assert it has saved it to disk and has been created correctly.
            var expectedWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(dataFolder.WalletPath + "/mywallet.wallet.json"));
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
                Assert.Equal(1, accountRoot.Accounts.Count);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                                                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var password = "test";

            var mnemonicList = new Mnemonic(Wordlist.French, WordCount.Eighteen);

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "mywallet", mnemonicList: mnemonicList.ToString());

            Assert.Equal(mnemonic.DeriveSeed(), mnemonicList.DeriveSeed());
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            var walletManager = new WalletManager(loggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var concurrentChain = new ConcurrentChain(Network.Main);
            ChainedBlock tip = WalletTestsHelpers.AppendBlock(null, concurrentChain).ChainedBlock;

            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet2"));

            Parallel.For(0, 500, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.UpdateLastBlockSyncedHeight(tip);
                walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet"));
                walletManager.UpdateLastBlockSyncedHeight(tip);
            });

            Assert.Equal(502, walletManager.Wallets.Count);
            Assert.True(walletManager.Wallets.All(w => w.BlockLocator != null));
        }

        [Fact]
        public void LoadWalletWithExistingWalletLoadsWalletOntoManager()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");

            File.WriteAllText(Path.Combine(dataFolder.WalletPath, "testWallet.wallet.json"), JsonConvert.SerializeObject(wallet, Formatting.Indented, new ByteArrayConverter()));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.LoadWallet("password", "testWallet");

            Assert.Equal("testWallet", result.Name);
            Assert.Equal(Network.Main, result.Network);

            Assert.Single(walletManager.Wallets);
            Assert.Equal("testWallet", walletManager.Wallets.ElementAt(0).Name);
            Assert.Equal(Network.Main, walletManager.Wallets.ElementAt(0).Network);
        }

        [Fact]
        public void LoadWalletWithNonExistingWalletThrowsFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() =>
           {
               DataFolder dataFolder = CreateDataFolder(this);

               var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

               walletManager.LoadWallet("password", "testWallet");
           });
        }

        [Fact]
        public void RecoverWalletWithEqualInputAsExistingWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var password = "test";
            var passphrase = "this is my magic passphrase";
            var walletName = "mywallet";

            ConcurrentChain chain = WalletTestsHelpers.PrepareChainWithBlock();
            // Prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(dataFolder, password, passphrase, walletName, chain);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Try to recover it.
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

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var password = "test";
            var walletName = "mywallet";

            ConcurrentChain chain = WalletTestsHelpers.PrepareChainWithBlock();
            // prepare an existing wallet through this manager and delete the file from disk. Return the created wallet object and mnemonic.
            var deletedWallet = this.CreateWalletOnDiskAndDeleteWallet(dataFolder, password, password, walletName, chain);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/{walletName}.wallet.json")));

            // create a fresh manager.
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

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

                Assert.Equal(1, recoveredAccountRoot.Accounts.Count);
                Assert.Equal(1, expectedAccountRoot.Accounts.Count);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet2"));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet3"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            Parallel.For(0, 5000, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.LoadKeysLookupLock();
                walletManager.LoadKeysLookupLock();
                walletManager.LoadKeysLookupLock();
            });

            Assert.Equal(240, walletManager.keysLookup.Count);
        }

        [Fact]
        public void GetUnusedAccountUsingNameForNonExistinAccountThrowsWalletException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetUnusedAccount("nonexisting", "password");
            });
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAccount("testWallet", "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAccount("testWallet", "password");

            Assert.Equal("account 0", result.Name);
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("account 0", result.Name);
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void CreateNewAccountGivenNoAccountsExistingInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var network = Network.Main;
            var walletManager = new WalletManager(this.LoggerFactory.Object, network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Clear();

            var result = wallet.AddNewAccount("password", (CoinType)network.Consensus.CoinType, DateTimeOffset.UtcNow);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 0", result.Name);
            Assert.Equal(0, result.Index);
            Assert.Equal($"m/44'/0'/0'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void CreateNewAccountGivenExistingAccountInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var network = Network.Main;
            var walletManager = new WalletManager(this.LoggerFactory.Object, network, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "unused" });

            var result = wallet.AddNewAccount("password", (CoinType)network.Consensus.CoinType, DateTimeOffset.UtcNow);

            Assert.Equal(2, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/1'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 1", result.Name);
            Assert.Equal(1, result.Index);
            Assert.Equal($"m/44'/0'/1'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(0, result.InternalAddresses.Count);
            Assert.Equal(0, result.ExternalAddresses.Count);
        }

        [Fact]
        public void GetUnusedAddressUsingNameWithWalletWithoutAccountOfGivenNameThrowsException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password");
                walletManager.Wallets.Add(wallet);

                var result = walletManager.GetUnusedAddress(new WalletAccountReference("testWallet", "unexistingAccount"));
            });
        }

        [Fact]
        public void GetUnusedAddressUsingNameForNonExistinAccountThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetUnusedAddress(new WalletAccountReference("nonexisting", "account"));
            });
        }

        [Fact]
        public void GetUnusedAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                ExternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = "myUsedAddress",
                        Transactions = new List<TransactionData>
                        {
                            new TransactionData()
                        }
                    },
                     new HdAddress {
                        Index = 1,
                        Address = "myUnusedAddress",
                        Transactions = new List<TransactionData>()
                    }
                },
                InternalAddresses = null
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            Assert.Equal("myUnusedAddress", result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            BitcoinSecret bob = new BitcoinSecret(new Key(), Network.RegTest);
            BitcoinSecret alice = new BitcoinSecret(new Key(), Network.RegTest);
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                InternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = bob.GetAddress().ToString(),
                        ScriptPubKey = bob.ScriptPubKey,
                        Transactions = new List<TransactionData>
                        {
                            new TransactionData()
                        }
                    },
                    new HdAddress {
                        Index = 1,
                        Address = alice.GetAddress().ToString(),
                        ScriptPubKey = alice.ScriptPubKey,
                        Transactions = new List<TransactionData>()
                    }
                },
                ExternalAddresses = new List<HdAddress>()
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.First().Accounts.First().Name));
            
            Assert.Equal(alice.GetAddress().ToString(), result.Address);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletNotHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountExtendedPubKey,
                InternalAddresses = new List<HdAddress>(),
                ExternalAddresses = new List<HdAddress>()
            });
            walletManager.Wallets.Add(wallet);
            
            var result = walletManager.GetUnusedChangeAddress(new WalletAccountReference(wallet.Name, wallet.AccountsRoot.First().Accounts.First().Name));

            Assert.NotNull(result.Address);
        }

        [Fact]
        public void GetUnusedAddressWithoutWalletHavingUnusedAddressCreatesAddressAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            var accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    new HdAddress {
                        Index = 0,
                        Address = "myUsedAddress",
                        ScriptPubKey = new Script(),
                        Transactions = new List<TransactionData>
                        {
                            new TransactionData()
                        },
                    }
                },
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = accountExtendedPubKey
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            KeyPath keyPath = new KeyPath($"0/1");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtendedPubKey).Derive(keyPath);
            var pubKey = extPubKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(wallet.Network);
            Assert.Equal(1, result.Index);
            Assert.Equal("m/44'/0'/0'/0/1", result.HdPath);
            Assert.Equal(address.ToString(), result.Address);
            Assert.Equal(pubKey.ScriptPubKey, result.Pubkey);
            Assert.Equal(address.ScriptPubKey, result.ScriptPubKey);
            Assert.Equal(0, result.Transactions.Count);
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/myWallet.wallet.json")));
        }

        [Fact]
        public void GetHistoryByNameWithExistingWalletReturnsAllAddressesWithTransactions()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedExternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedExternalAddress"),
                },
                InternalAddresses = new List<HdAddress> {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedInternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedInternalAddress"),
                },
                ExtendedPubKey = "blabla"
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetHistory("myWallet").ToList();

            Assert.NotEmpty(result);
            Assert.Single(result);
            var accountHistory = result.ElementAt(0);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            var historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal("myUsedExternalAddress", historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal("myUsedInternalAddress", historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithExistingAccountReturnsAllAddressesWithTransactions()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            HdAccount account = new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedExternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedExternalAddress"),
                },
                InternalAddresses = new List<HdAddress>
                {
                    WalletTestsHelpers.CreateAddressWithEmptyTransaction(0, "myUsedInternalAddress"),
                    WalletTestsHelpers.CreateAddressWithoutTransaction(1, "myUnusedInternalAddress"),
                },
                ExtendedPubKey = "blabla"
            };

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(account);
            walletManager.Wallets.Add(wallet);

            var accountHistory = walletManager.GetHistory(account);

            Assert.NotNull(accountHistory);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            var historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal("myUsedExternalAddress", historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal("myUsedInternalAddress", historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithoutHavingAddressesWithTransactionsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");

            HdAccount account = new HdAccount
            {
                Index = 0,
                Name = "myAccount",
                HdPath = "m/44'/0'/0'",
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                ExtendedPubKey = "blabla"
            };
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(account);
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetHistory(account);

            Assert.NotNull(result.Account);
            Assert.Equal("myAccount", result.Account.Name);
            Assert.Empty(result.History);
        }

        [Fact]
        public void GetHistoryByWalletNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.GetHistory("noname");
            });
        }

        [Fact]
        public void GetWalletByNameWithExistingWalletReturnsWallet()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetWallet("myWallet");

            Assert.Equal(wallet.EncryptedSeed, result.EncryptedSeed);
        }

        [Fact]
        public void GetWalletByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.GetWallet("noname");
            });
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletReturnsAccountsFromWallet()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 0" });
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 1" });
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = CoinType.Stratis,
                Accounts = new List<HdAccount> { new HdAccount { Name = "Account 2" } }
            });
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = CoinType.Bitcoin,
                Accounts = new List<HdAccount> { new HdAccount { Name = "Account 3" } }
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetAccounts("myWallet");

            Assert.Equal(3, result.Count());
            Assert.Equal("Account 0", result.ElementAt(0).Name);
            Assert.Equal("Account 1", result.ElementAt(1).Name);
            Assert.Equal("Account 3", result.ElementAt(2).Name);
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletMissingAccountsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.Clear();
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetAccounts("myWallet");

            Assert.Empty(result);
        }

        [Fact]
        public void GetAccountsByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetAccounts("myWallet");
            });
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsReturnsChainTipHeight()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(chain.Tip.Height, result);
        }

        [Fact]
        public void LastBlockHeightWithWalletsReturnsLowestLastBlockSyncedHeightForAccountRootsOfManagerCoinType()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet3.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(20, result);
        }

        [Fact]
        public void LastBlockHeightWithWalletsReturnsLowestLastBlockSyncedHeightForAccountRootsOfManagerCoinType2()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = CoinType.Bitcoin,
                LastBlockSyncedHeight = 12
            });

            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet3.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(12, result);
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsOfCoinTypeReturnsZero()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            walletManager.Wallets.Add(wallet);

            var result = walletManager.LastBlockHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void LastReceivedBlockHashWithoutWalletsReturnsChainTipHashBlock()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            var nonce = RandomUtils.GetUInt32();
            var block = new Block();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.LastReceivedBlockHash();

            Assert.Equal(chain.Tip.HashBlock, result);
        }

        [Fact]
        public void LastReceivedBlockHashWithWalletsReturnsLowestLastBlockSyncedHashForAccountRootsOfManagerCoinType()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(15);
            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(20);
            var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet3.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(56);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            var result = walletManager.LastReceivedBlockHash();

            Assert.Equal(new uint256(20), result);
        }

        [Fact]
        public void LastReceivedBlockHashWithWalletsReturnsLowestLastReceivedBlockHashForAccountRootsOfManagerCoinType2()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 15;
            wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(15);
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = CoinType.Bitcoin,
                LastBlockSyncedHeight = 12,
                LastBlockSyncedHash = new uint256(12)
            });

            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 20;
            wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(20);
            var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet3.AccountsRoot.ElementAt(0).CoinType = CoinType.Bitcoin;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHeight = 56;
            wallet3.AccountsRoot.ElementAt(0).LastBlockSyncedHash = new uint256(56);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            var result = walletManager.LastReceivedBlockHash();

            Assert.Equal(new uint256(12), result);
        }

        [Fact]
        public void NoLastReceivedBlockHashInWalletReturnsChainTip()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(2, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            walletManager.Wallets.Add(wallet);

            var result = walletManager.LastReceivedBlockHash();
            Assert.Equal(chain.Tip.HashBlock, result);
        }

        [Fact]
        public void GetSpendableTransactionsWithChainOfHeightZeroReturnsNoTransactions()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(0, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 2, 9, 10)
            });

            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);

            Assert.Empty(result);
        }

        /// <summary>
        /// If the block height of the transaction is x+ away from the current chain top transactions must be returned where x is higher or equal to the specified amount of confirmations.
        /// </summary>
        [Fact]
        public void GetSpendableTransactionsReturnsTransactionsGivenBlockHeight()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 2, 9, 10)
            });

            wallet.AccountsRoot.Add(new AccountRoot()
            {
                CoinType = CoinType.Stratis,
                Accounts = new List<HdAccount>
                {
                    new HdAccount {
                        ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 8,9,10),
                        InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 8,9,10)
                    }
                }
            });

            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");
            wallet2.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet2.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 1, 3, 5, 7, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 2, 4, 6, 8, 9, 10)
            });

            var wallet3 = this.walletFixture.GenerateBlankWallet("myWallet3", "password");
            wallet3.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "Second expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 5, 9, 11),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 6, 9, 11)
            });

            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.Wallets.Add(wallet3);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet3", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            var info = result[0];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(5, info.Transaction.BlockHeight);
            info = result[1];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            info = result[2];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(6, info.Transaction.BlockHeight);
            info = result[3];
            Assert.Equal("Second expectation", info.Account.Name);
            Assert.Equal(wallet3.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
        }

        [Fact]
        public void GetSpendableTransactionsWithSpentTransactionsReturnsSpendableTransactionsGivenBlockHeight()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 1, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 9, 11)).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.Main, 2, 9, 11).Concat(WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 2, 9, 11)).ToList()
            });

            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            var info = result[0];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(1, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[1];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[2];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Address, info.Address.Address);
            Assert.Equal(2, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
            info = result[3];
            Assert.Equal("First expectation", info.Account.Name);
            Assert.Equal(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Address, info.Address.Address);
            Assert.Equal(9, info.Transaction.BlockHeight);
            Assert.Null(info.Transaction.SpendingDetails);
        }

        [Fact]
        public void GetSpendableTransactionsWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var chain = WalletTestsHelpers.GenerateChainWithHeight(10, Network.Main);
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);
            });
        }

        [Fact]
        public void GetSpendableTransactionsWithoutWalletsOfWalletManagerCoinTypeReturnsEmptyList()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet2", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                ExternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 1, 3, 5, 7, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(Network.StratisMain, 2, 4, 6, 8, 9, 10)
            });
            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet2", confirmations: 1);

            Assert.Empty(result);
        }

        [Fact]
        public void GetSpendableTransactionsWithOnlySpentTransactionsReturnsEmptyList()
        {
            var chain = WalletTestsHelpers.GenerateChainWithHeight(10, Network.Main);
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First expectation",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 9, 10),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 2, 9, 10)
            });

            walletManager.Wallets.Add(wallet);

            var result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1);

            Assert.Empty(result);
        }

        [Fact]
        public void GetKeyForAddressWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

                var wallet = walletManager.GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("password", new HdAddress()).PrivateKey;
            });
        }

        [Fact]
        public void GetKeyForAddressWithWalletReturnsAddressExtPrivateKey()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password");

            var address = new HdAddress
            {
                Index = 0,
                HdPath = "m/44'/0'/0'/0/0",
            };

            data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                ExternalAddresses = new List<HdAddress> {
                    address
                },
                InternalAddresses = new List<HdAddress>(),
                Name = "savings account"
            });
            walletManager.Wallets.Add(data.wallet);

            var result = data.wallet.GetExtendedPrivateKeyForAddress("password", address);

            Assert.Equal(data.key.Derive(new KeyPath("m/44'/0'/0'/0/0")).GetWif(data.wallet.Network), result);
        }

        [Fact]
        public void GetKeyForAddressWitoutAddressOnWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var data = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password");

                var address = new HdAddress
                {
                    Index = 0,
                    HdPath = "m/44'/0'/0'/0/0",
                };

                data.wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
                {
                    Index = 0,
                    ExternalAddresses = new List<HdAddress>(),
                    InternalAddresses = new List<HdAddress>(),
                    Name = "savings account"
                });
                walletManager.Wallets.Add(data.wallet);

                data.wallet.GetExtendedPrivateKeyForAddress("password", address);
            });
        }

        [Fact]
        public void ProcessTransactionWithValidTransactionLoadsTransactionsIntoWalletIfMatching()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();
            walletManager.ProcessTransaction(transaction);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithEmptyScriptInTransactionDoesNotAddTransactionToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            transaction.Outputs.ElementAt(1).Value = Money.Zero;
            transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();
            walletManager.ProcessTransaction(transaction);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

            Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationToChangeAddressDoesNotAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/1");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationChangeAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/1/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress },
                InternalAddresses = new List<HdAddress> { changeAddress, destinationChangeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();

            walletManager.ProcessTransaction(transaction);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);
            Assert.Equal(1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockHeightSetsBlockHeightOnTransactionData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();

            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            var blockHeight = chainInfo.chain.GetBlock(block.GetHash()).Height;
            walletManager.ProcessTransaction(transaction, blockHeight);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(blockHeight - 1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockSetsBlockHash()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            // setup a payment to yourself
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();

            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            walletManager.ProcessTransaction(transaction, block: block);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(chainInfo.block.GetHash(), spentAddressResult.Transactions.ElementAt(0).BlockHash);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithoutMempoolValidatorProcessesTransactionAndBroadcastsTransactionToConnectionManagerNodes()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //}
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolSuccessProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.True(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
        //        Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

        //        Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
        //        Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
        //        Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
        //        Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

        //        Assert.Equal(1, payloads.Count);
        //        Assert.Equal(typeof(TxPayload), payloads[0].GetType());

        //        var payload = payloads[0] as TxPayload;
        //        var payloadTransaction = payload.Object;
        //        Assert.Equal(transaction.ToHex(), payloadTransaction.ToHex());
        //    }
        //}
        //}

        /// <summary>
        /// TODO: [SENDTRANSACTION] Conceptual changes had been introduced to tx sending.
        /// <para>
        /// These tests don't make sense anymore, it must be either removed or refactored.
        /// </para>
        /// </summary>
        //[Fact(Skip = "See TODO")]
        //public void SendTransactionWithMempoolValidatorWithAcceptToMemoryPoolFailedDoesNotProcessesTransaction()
        //{
        //DataFolder dataFolder = CreateDataFolder(this);
        //Directory.CreateDirectory(dataFolder.WalletPath);

        //var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
        //var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
        //var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
        //var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
        //var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

        //var spendingAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/0/0",
        //    Address = spendingKeys.Address.ToString(),
        //    Pubkey = spendingKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = spendingKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var destinationAddress = new HdAddress
        //{
        //    Index = 1,
        //    HdPath = $"m/44'/0'/0'/0/1",
        //    Address = destinationKeys.Address.ToString(),
        //    Pubkey = destinationKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = destinationKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        //var changeAddress = new HdAddress
        //{
        //    Index = 0,
        //    HdPath = $"m/44'/0'/0'/1/0",
        //    Address = changeKeys.Address.ToString(),
        //    Pubkey = changeKeys.PubKey.ScriptPubKey,
        //    ScriptPubKey = changeKeys.Address.ScriptPubKey,
        //    Transactions = new List<TransactionData>()
        //};

        ////Generate a spendable transaction
        //var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
        //TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
        //spendingAddress.Transactions.Add(spendingTransaction);

        //wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
        //{
        //    Index = 0,
        //    Name = "account1",
        //    HdPath = "m/44'/0'/0'",
        //    ExtendedPubKey = accountKeys.ExtPubKey,
        //    ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
        //    InternalAddresses = new List<HdAddress> { changeAddress }
        //});

        //// setup a payment to yourself
        //var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
        //transaction.Outputs.ElementAt(1).Value = Money.Zero;
        //transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

        //var walletFeePolicy = new Mock<IWalletFeePolicy>();
        //walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
        //    .Returns(new Money(5000));

        //using (var nodeSocket = new NodeTcpListenerStub(Utils.ParseIpEndpoint("localhost", wallet.Network.DefaultPort)))
        //{
        //    using (var node = Node.ConnectToLocal(wallet.Network, new NodeConnectionParameters()))
        //    {
        //        var payloads = new List<Payload>();
        //        node.Filters.Add(new Action<IncomingMessage, Action>((i, a) => { a(); }),
        //                  new Action<Node, Payload, Action>((n, p, a) => { payloads.Add(p); a(); }));

        //        var nodeCollection = new NodesCollection();
        //        nodeCollection.Add(node);

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
        //        walletManager.Wallets.Add(wallet);

        //        var result = walletManager.SendTransaction(transaction.ToHex());

        //        Assert.False(result);
        //        // verify AcceptToMemoryPool has been called.
        //        mempoolValidator.Verify();

        //        var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
        //        Assert.Equal(1, spendingAddress.Transactions.Count);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Null(spentAddressResult.Transactions.ElementAt(0).SpendingDetails);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
        //        Assert.Equal(0, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
        //        Assert.Equal(0, payloads.Count);
        //    }
        //}
        //}

        [Fact]
        public void RemoveBlocksRemovesTransactionsWithHigherBlockHeightAndUpdatesLastSyncedBlockHeight()
        {
            var trxId = uint256.Parse("21e74d1daed6dec93d58396a3406803c5fc8d220b59f4b4dd185cab5f7a9a22e");
            int trxCount = 0;
            var concurrentchain = new ConcurrentChain(Network.Main);
            var chainedBlock = WalletTestsHelpers.AppendBlock(null, concurrentchain).ChainedBlock;
            chainedBlock = WalletTestsHelpers.AppendBlock(chainedBlock, concurrentchain).ChainedBlock;
            chainedBlock = WalletTestsHelpers.AppendBlock(chainedBlock, concurrentchain).ChainedBlock;

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First account",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3, 4, 5).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3, 4, 5).ToList()
            });

            // reorg at block 3

            // Trx at block 0 is not spent
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().Id = trxId >> trxCount++; ;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.First().Id = trxId >> trxCount++;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;

            // Trx at block 2 is spent in block 3, after reorg it will not be spendable.
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;

            // Trx at block 3 is spent at block 5, after reorg it will be spendable.
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++; ;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++; ;
            wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();
            walletManager.RemoveBlocks(chainedBlock);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);

            var account = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);

            Assert.Equal(6, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).Count());
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.BlockHeight <= chainedBlock.Height));
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.SpendingDetails == null || r.SpendingDetails.BlockHeight <= chainedBlock.Height));
            Assert.Equal(4, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).Count(t => t.SpendingDetails == null));
        }

        [Fact]
        public void ProcessBlockWithoutWalletsSetsWalletTipToBlockHash()
        {
            var concurrentchain = new ConcurrentChain(Network.Main);
            var blockResult = WalletTestsHelpers.AppendBlock(null, concurrentchain);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.ProcessBlock(blockResult.Block, blockResult.ChainedBlock);

            Assert.Equal(blockResult.ChainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletsProcessesTransactionsOfBlockToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey,
                Transactions = new List<TransactionData>()
            };

            //Generate a spendable transaction
            var chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);

            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            // setup a payment to yourself in a new block.
            var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Index = 0,
                Name = "account1",
                HdPath = "m/44'/0'/0'",
                ExtendedPubKey = accountKeys.ExtPubKey,
                ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
                InternalAddresses = new List<HdAddress> { changeAddress }
            });

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.LoadKeysLookupLock();
            walletManager.WalletTipHash = block.Header.GetHash();

            var chainedBlock = chainInfo.chain.GetBlock(block.GetHash());
            walletManager.ProcessBlock(block, chainedBlock);

            var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Equal(1, spendingAddress.Transactions.Count);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
            var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
            var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletTipBlockNotOnChainYetThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
                var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.Wallets.Add(wallet);

                walletManager.WalletTipHash = new uint256(15012522521);

                walletManager.ProcessBlock(chainResult.Block, chainResult.ChainedBlock);
            });
        }

        [Fact]
        public void ProcessBlockWithBlockAheadOfWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

                ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
                var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);
                var chainResult2 = WalletTestsHelpers.AppendBlock(chainResult.ChainedBlock, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                walletManager.Wallets.Add(wallet);

                walletManager.WalletTipHash = wallet.Network.GetGenesis().Header.GetHash();

                walletManager.ProcessBlock(chainResult2.Block, chainResult2.ChainedBlock);
            });
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

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
        public void GetAccountBalancesReturnsCorrectAccountBalances()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            
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

            Wallet wallet = WalletTestsHelpers.CreateWallet("myWallet");
            wallet.AccountsRoot.Add(new AccountRoot());
            wallet.AccountsRoot.First().Accounts = accounts;

            walletManager.Wallets.Add(wallet);

            // Act.
            var balances = walletManager.GetBalances("myWallet");

            // Assert.
            AccountBalance resultingBalance = balances.First();
            Assert.Equal(account.Name, resultingBalance.Account.Name);
            Assert.Equal(account.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(130000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(35000), resultingBalance.AmountUnconfirmed);

            resultingBalance = balances.ElementAt(1);
            Assert.Equal(account2.Name, resultingBalance.Account.Name);
            Assert.Equal(account2.HdPath, resultingBalance.Account.HdPath);
            Assert.Equal(new Money(108000), resultingBalance.AmountConfirmed);
            Assert.Equal(new Money(139000), resultingBalance.AmountUnconfirmed);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

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
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1"));
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

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

        [Fact]
        public void SaveToFileWithoutWalletParameterSavesAllWalletsOnManagerToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            var wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.SaveWallets();

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);

            var resultWallet2 = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));
            Assert.Equal(wallet2.Name, resultWallet2.Name);
            Assert.Equal(wallet2.EncryptedSeed, resultWallet2.EncryptedSeed);
            Assert.Equal(wallet2.ChainCode, resultWallet2.ChainCode);
            Assert.Equal(wallet2.Network, resultWallet2.Network);
            Assert.Equal(wallet2.AccountsRoot.Count, resultWallet2.AccountsRoot.Count);
        }

        [Fact]
        public void SaveToFileWithWalletParameterSavesGivenWalletToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            var wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.SaveWallet(wallet);

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);
        }

        [Fact]
        public void GetWalletFileExtensionReturnsWalletExtension()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.GetWalletFileExtension();

            Assert.Equal("wallet.json", result);
        }

        [Fact]
        public void GetWalletsReturnsLoadedWalletNames()
        {
            var wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            var wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            var result = walletManager.GetWalletsNames().OrderBy(w => w).ToArray();

            Assert.Equal(2, result.Count());
            Assert.Equal("wallet1", result[0]);
            Assert.Equal("wallet2", result[1]);
        }

        [Fact]
        public void GetWalletsWithoutLoadedWalletsReturnsEmptyList()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var result = walletManager.GetWalletsNames().OrderBy(w => w);

            Assert.Empty(result);
        }

        [Fact]
        public void LoadKeysLookupWithKeysLoadsKeyLookup()
        {
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
            {
                Name = "First account",
                ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3).ToList(),
                InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3).ToList()
            });

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);

            walletManager.LoadKeysLookupLock();

            Assert.NotNull(walletManager.keysLookup);
            Assert.Equal(6, walletManager.keysLookup.Count);

            var externalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses;
            Assert.Equal(externalAddresses.ElementAt(0).Address, walletManager.keysLookup[externalAddresses.ElementAt(0).ScriptPubKey].Address);
            Assert.Equal(externalAddresses.ElementAt(1).Address, walletManager.keysLookup[externalAddresses.ElementAt(1).ScriptPubKey].Address);
            Assert.Equal(externalAddresses.ElementAt(2).Address, walletManager.keysLookup[externalAddresses.ElementAt(2).ScriptPubKey].Address);

            var internalAddresses = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses;
            Assert.Equal(internalAddresses.ElementAt(0).Address, walletManager.keysLookup[internalAddresses.ElementAt(0).ScriptPubKey].Address);
            Assert.Equal(internalAddresses.ElementAt(1).Address, walletManager.keysLookup[internalAddresses.ElementAt(1).ScriptPubKey].Address);
            Assert.Equal(internalAddresses.ElementAt(2).Address, walletManager.keysLookup[internalAddresses.ElementAt(2).ScriptPubKey].Address);
        }

        [Fact]
        public void LoadKeysLookupWithoutWalletsInitializesEmptyDictionary()
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            walletManager.LoadKeysLookupLock();

            Assert.NotNull(walletManager.keysLookup);
            Assert.Empty(walletManager.keysLookup);
        }

        [Fact]
        public void CreateBip44PathWithChangeAddressReturnsPath()
        {
            var result = HdOperations.CreateHdPath((int)CoinType.Stratis, 4, 3, true);

            Assert.Equal("m/44'/105'/4'/1/3", result);
        }

        [Fact]
        public void CreateBip44PathWithoutChangeAddressReturnsPath()
        {
            var result = HdOperations.CreateHdPath((int)CoinType.Stratis, 4, 3, false);

            Assert.Equal("m/44'/105'/4'/0/3", result);
        }

        [Fact]
        public void StopSavesWallets()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            var wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test");
            var wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test");

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);

            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            walletManager.Stop();

            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.True(File.Exists(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));

            var resultWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet1.wallet.json")));
            Assert.Equal(wallet.Name, resultWallet.Name);
            Assert.Equal(wallet.EncryptedSeed, resultWallet.EncryptedSeed);
            Assert.Equal(wallet.ChainCode, resultWallet.ChainCode);
            Assert.Equal(wallet.Network, resultWallet.Network);
            Assert.Equal(wallet.AccountsRoot.Count, resultWallet.AccountsRoot.Count);

            var resultWallet2 = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(dataFolder.WalletPath + $"/wallet2.wallet.json")));
            Assert.Equal(wallet2.Name, resultWallet2.Name);
            Assert.Equal(wallet2.EncryptedSeed, resultWallet2.EncryptedSeed);
            Assert.Equal(wallet2.ChainCode, resultWallet2.ChainCode);
            Assert.Equal(wallet2.Network, resultWallet2.Network);
            Assert.Equal(wallet2.AccountsRoot.Count, resultWallet2.AccountsRoot.Count);
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithChainedBlockUpdatesWallets()
        {
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");

            ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            var chainedBlock = WalletTestsHelpers.AppendBlock(chain.Genesis, chain).ChainedBlock;

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(chainedBlock);

            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
            foreach (var w in walletManager.Wallets)
            {
                Assert.Equal(chainedBlock.GetLocator().Blocks, w.BlockLocator);
                Assert.Equal(chainedBlock.Height, w.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
                Assert.Equal(chainedBlock.HashBlock, w.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            }
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithWalletAndChainedBlockUpdatesGivenWallet()
        {
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            var wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password");

            ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            var chainedBlock = WalletTestsHelpers.AppendBlock(chain.Genesis, chain).ChainedBlock;

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.Wallets.Add(wallet2);
            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(wallet, chainedBlock);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.NotEqual(chainedBlock.HashBlock, walletManager.WalletTipHash);

            Assert.NotEqual(chainedBlock.GetLocator().Blocks, wallet2.BlockLocator);
            Assert.NotEqual(chainedBlock.Height, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.NotEqual(chainedBlock.HashBlock, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithWalletAccountRootOfDifferentCoinTypeDoesNotUpdateLastSyncedInformation()
        {
            var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;

            ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
            var chainedBlock = WalletTestsHelpers.AppendBlock(chain.Genesis, chain).ChainedBlock;

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            walletManager.Wallets.Add(wallet);
            walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(wallet, chainedBlock);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.NotEqual(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.NotEqual(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.NotEqual(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void RemoveAllTransactionsInWalletReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            for (int i = 0; i < 3; i++)
            {
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
            }

            // Add two confirmed transactions.
            for (int i = 3; i < 6; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
            }

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(12, transactionCount);

            // Act.
            var result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions));
            Assert.Equal(12, result.Count);
        }

        [Fact]
        public void RemoveAllTransactionsWhenNoTransactionsArePresentReturnsEmptyList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and no transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(0, transactionCount);

            // Act.
            var result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions));
            Assert.Empty(result);
        }

        [Fact]
        public void RemoveTransactionsByIdsWhenTransactionsAreUnconfirmedReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            TransactionData trxUnconfirmed1 = new TransactionData {Amount = 10, Id = trxId >> counter++ };
            TransactionData trxUnconfirmed2 = new TransactionData {Amount = 10, Id = trxId >> counter++ };
            TransactionData trxConfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50000 };
            TransactionData trxConfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50001 };

            firstAccount.ExternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            firstAccount.ExternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            firstAccount.InternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed2);
            firstAccount.InternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(4, transactionCount);

            // Act.
            var result = walletManager.RemoveTransactionsByIds("wallet1", new [] {trxUnconfirmed1.Id, trxUnconfirmed2.Id, trxConfirmed1.Id, trxConfirmed2.Id });

            // Assert.
            var remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count());
            Assert.Equal(2, result.Count);
            Assert.Contains((trxUnconfirmed1.Id, trxConfirmed1.CreationTime), result);
            Assert.Contains((trxUnconfirmed2.Id, trxConfirmed2.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.DoesNotContain(trxUnconfirmed2, remainingTrxs);
        }

        [Fact]
        public void RemoveTransactionsByIdsAlsoRemovesUnconfirmedSpendingDetailsTransactions()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1");
            walletManager.Wallets.Add(wallet);
            WalletTestsHelpers.AddAddressesToWallet(walletManager, 20);

            var firstAccount = wallet.AccountsRoot.First().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            // Confirmed transaction with confirmed spending.
            SpendingDetails confirmedSpendingDetails = new SpendingDetails { TransactionId = trxId >> counter++, BlockHeight = 500002 };
            TransactionData trxConfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50000, SpendingDetails = confirmedSpendingDetails };

            // Confirmed transaction with unconfirmed spending.
            uint256 unconfirmedTransactionId = trxId >> counter++;
            SpendingDetails unconfirmedSpendingDetails1 = new SpendingDetails { TransactionId = unconfirmedTransactionId };
            TransactionData trxConfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50001, SpendingDetails = unconfirmedSpendingDetails1 };
            
            // Unconfirmed transaction.
            TransactionData trxUnconfirmed1 = new TransactionData { Amount = 10, Id = unconfirmedTransactionId };
            
            firstAccount.ExternalAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            firstAccount.ExternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            firstAccount.InternalAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            var transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(3, transactionCount);

            // Act.
            var result = walletManager.RemoveTransactionsByIds("wallet1", new[]
            {
                trxConfirmed1.Id, // Shouldn't be removed.
                unconfirmedTransactionId, // A transaction + a spending transaction should be removed.
                trxConfirmed2.Id, // Shouldn't be removed.
                confirmedSpendingDetails.TransactionId, // Shouldn't be removed.
            });

            // Assert.
            var remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count);
            Assert.Single(result);
            Assert.Contains((unconfirmedTransactionId, trxUnconfirmed1.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.Null(trxConfirmed2.SpendingDetails);
        }

        private (Mnemonic mnemonic, Wallet wallet) CreateWalletOnDiskAndDeleteWallet(DataFolder dataFolder, string password, string passphrase, string walletName, ConcurrentChain chain)
        {
            var walletManager = new WalletManager(this.LoggerFactory.Object, Network.StratisMain, chain, NodeSettings.Default(), new Mock<WalletSettings>().Object,
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, walletName, passphrase);
            var wallet = walletManager.Wallets.ElementAt(0);

            File.Delete(dataFolder.WalletPath + $"/{walletName}.wallet.json");

            return (mnemonic, wallet);
        }
    }

    public class WalletFixture : IDisposable
    {
        private readonly Dictionary<(string, string), Wallet> walletsGenerated;

        public WalletFixture()
        {
            this.walletsGenerated = new Dictionary<(string, string), Wallet>();
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Creates a new wallet.
        /// </summary>
        /// <remarks>
        /// If it's the first time this wallet is created within this class, it is added to a collection for use by other tests.
        /// If the same parameters have already been used to create a wallet, the wallet will be retrieved from the internal collection and a copy of this wallet will be returned.
        /// </remarks>
        /// <param name="name">The name.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        public Wallet GenerateBlankWallet(string name, string password)
        {
            if (this.walletsGenerated.TryGetValue((name, password), out Wallet existingWallet))
            {
                string serializedExistingWallet = JsonConvert.SerializeObject(existingWallet, Formatting.None);
                return JsonConvert.DeserializeObject<Wallet>(serializedExistingWallet);
            }

            Wallet newWallet = WalletTestsHelpers.GenerateBlankWallet(name, password);
            this.walletsGenerated.Add((name, password), newWallet);

            string serializedNewWallet = JsonConvert.SerializeObject(newWallet, Formatting.None);
            return JsonConvert.DeserializeObject<Wallet>(serializedNewWallet);
        }
    }
}
