using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly IBlockStore blockStore;
        private readonly WalletFixture walletFixture;

        public WalletManagerTest(WalletFixture walletFixture)
        {
            this.blockStore = new Mock<IBlockStore>().Object;
            this.walletFixture = walletFixture;
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithoutPassphraseOrMnemonicCreatesWalletUsingPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            string password = "test";
            string passphrase = "test";

            // create the wallet
            (_, Mnemonic mnemonic) = walletManager.CreateWallet(password, "mywallet", passphrase);

            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            walletManager.ProcessBlock(block, chain.Tip);

            // assert it has saved it to disk and has been created correctly.
            Wallet actualWallet = walletManager.Wallets.ElementAt(0);

            Assert.Equal("mywallet", actualWallet.Name);
            Assert.Equal(KnownNetworks.StratisMain, actualWallet.Network);

            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (int i = 0; i < actualWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.Stratis, actualWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(1, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(block.GetHash(), actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                AccountRoot accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Single(accountRoot.Accounts);

                for (int j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    HdAccount actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(actualWallet.EncryptedSeed, "test", actualWallet.Network), actualWallet.ChainCode);
                    string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(actualWallet.Network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (int k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(actualWallet.Network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                        Assert.Empty(actualAddress.Transactions);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (int l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(actualWallet.Network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                        Assert.Empty(actualAddress.Transactions);
                    }
                }
            }

            Assert.Equal(2, actualWallet.BlockLocator.Count);

            uint256 expectedBlockHash = block.GetHash();
            Assert.Equal(expectedBlockHash, actualWallet.BlockLocator.ElementAt(0));
            Assert.Equal(actualWallet.BlockLocator.ElementAt(0), actualWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, actualWallet.BlockLocator.ElementAt(1));
            Assert.Equal(actualWallet.BlockLocator.ElementAt(1), actualWallet.BlockLocator.ElementAt(1));

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(password).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
        }

        /// <summary>
        /// This is more of an integration test to verify fields are filled correctly. This is what I could confirm.
        /// </summary>
        [Fact]
        public void CreateWalletWithPasswordAndPassphraseCreatesWalletUsingPasswordAndPassphrase()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);

            var network = KnownNetworks.StratisMain;

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, network, chain, new WalletSettings(NodeSettings.Default(network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            string password = "test";
            string passphrase = "this is my magic passphrase";

            // create the wallet
            (_, Mnemonic mnemonic) = walletManager.CreateWallet(password, "mywallet", passphrase);

            var block1 = network.CreateBlock();
            block1.AddTransaction(new Transaction());
            block1.UpdateMerkleRoot();
            block1.Header.HashPrevBlock = chain.Tip.HashBlock;
            block1.Header.Nonce = RandomUtils.GetUInt32();
            block1.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block1.Header);

            walletManager.ProcessBlock(block1, chain.Tip);

            // assert it has saved it to disk and has been created correctly.
            Wallet actualWallet = walletManager.Wallets.ElementAt(0);

            Assert.Equal("mywallet", actualWallet.Name);
            Assert.Equal(KnownNetworks.StratisMain, actualWallet.Network);

            Assert.Equal(1, actualWallet.AccountsRoot.Count);

            for (int i = 0; i < actualWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(CoinType.Stratis, actualWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(chain.Tip.Height, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(chain.Tip.HashBlock, actualWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                AccountRoot accountRoot = actualWallet.AccountsRoot.ElementAt(i);
                Assert.Single(accountRoot.Accounts);

                for (int j = 0; j < accountRoot.Accounts.Count; j++)
                {
                    HdAccount actualAccount = accountRoot.Accounts.ElementAt(j);
                    Assert.Equal($"account {j}", actualAccount.Name);
                    Assert.Equal(j, actualAccount.Index);
                    Assert.Equal($"m/44'/105'/{j}'", actualAccount.HdPath);

                    var extKey = new ExtKey(Key.Parse(actualWallet.EncryptedSeed, "test", network), actualWallet.ChainCode);
                    string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/105'/{j}'")).Neuter().ToString(network);
                    Assert.Equal(expectedExtendedPubKey, actualAccount.ExtendedPubKey);

                    Assert.Equal(20, actualAccount.InternalAddresses.Count);

                    for (int k = 0; k < actualAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress actualAddress = actualAccount.InternalAddresses.ElementAt(k);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"1/{k}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(network);
                        Assert.Equal(k, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/1/{k}", actualAddress.HdPath);
                        Assert.Empty(actualAddress.Transactions);
                    }

                    Assert.Equal(20, actualAccount.ExternalAddresses.Count);
                    for (int l = 0; l < actualAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress actualAddress = actualAccount.ExternalAddresses.ElementAt(l);
                        PubKey expectedAddressPubKey = ExtPubKey.Parse(expectedExtendedPubKey).Derive(new KeyPath($"0/{l}")).PubKey;
                        BitcoinPubKeyAddress expectedAddress = expectedAddressPubKey.GetAddress(network);
                        Assert.Equal(l, actualAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, actualAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.ToString(), actualAddress.Address);
                        Assert.Equal(expectedAddressPubKey.ScriptPubKey, actualAddress.Pubkey);
                        Assert.Equal($"m/44'/105'/{j}'/0/{l}", actualAddress.HdPath);
                        Assert.Empty(actualAddress.Transactions);
                    }
                }
            }

            Assert.Equal(2, actualWallet.BlockLocator.Count);

            uint256 expectedBlockHash = block1.GetHash();
            Assert.Equal(expectedBlockHash, actualWallet.BlockLocator.ElementAt(0));
            Assert.Equal(actualWallet.BlockLocator.ElementAt(0), actualWallet.BlockLocator.ElementAt(0));

            expectedBlockHash = chain.Genesis.HashBlock;
            Assert.Equal(expectedBlockHash, actualWallet.BlockLocator.ElementAt(1));
            Assert.Equal(actualWallet.BlockLocator.ElementAt(1), actualWallet.BlockLocator.ElementAt(1));

            Assert.Equal(actualWallet.EncryptedSeed, mnemonic.DeriveExtKey(passphrase).PrivateKey.GetEncryptedBitcoinSecret(password, KnownNetworks.StratisMain).ToWif());
        }

        [Fact]
        public void CreateWalletWithMnemonicListCreatesWalletUsingMnemonicList()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                                                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            string password = "test";
            string passphrase = "this is my magic passphrase";

            var mnemonic = new Mnemonic(Wordlist.French, WordCount.Eighteen);

            (_, Mnemonic returnedMnemonic) = walletManager.CreateWallet(password, "mywallet", passphrase, mnemonic);

            Assert.Equal(mnemonic.DeriveSeed(), returnedMnemonic.DeriveSeed());
        }

        [Fact]
        public void CreateWalletWithWalletSetting100UnusedAddressBufferCreates100AddressesToMonitor()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = this.CreateWalletManager(dataFolder, this.Network, "-walletaddressbuffer=100");

            walletManager.Start();

            walletManager.CreateWallet("test", "mywallet", "this is my magic passphrase", new Mnemonic(Wordlist.English, WordCount.Eighteen));

            HdAccount hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();

            Assert.Equal(100, hdAccount.ExternalAddresses.Count);
            Assert.Equal(100, hdAccount.InternalAddresses.Count);
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(loggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            var concurrentChain = new ChainIndexer(this.Network);
            ChainedHeader tip = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentChain).ChainedHeader;

            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet1", walletRepository));
            walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet2", walletRepository));

            Parallel.For(0, 500, new ParallelOptions { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.UpdateLastBlockSyncedHeight(tip);
                walletManager.Wallets.Add(WalletTestsHelpers.CreateWallet("wallet" + (3 + iteration), walletRepository));
                walletManager.UpdateLastBlockSyncedHeight(tip);
            });

            Assert.Equal(502, walletManager.Wallets.Count);
            Assert.True(walletManager.Wallets.All(w => w.BlockLocator != null));
        }

        [Fact]
        public void LoadWalletWithExistingWalletLoadsWalletOntoManager()
        {
            var network = KnownNetworks.StratisMain;

            DataFolder dataFolder = CreateDataFolder(this, network: network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);

            walletManager.Stop();

            walletManager.Start();

            Wallet result = walletManager.LoadWallet("password", "testWallet");

            Assert.Equal("testWallet", result.Name);
            Assert.Equal(network, result.Network);

            Assert.Single(walletManager.Wallets);
            Assert.Equal("testWallet", walletManager.Wallets.ElementAt(0).Name);
            Assert.Equal(network, walletManager.Wallets.ElementAt(0).Network);
        }

        [Fact]
        public void LoadWalletWithNonExistingWalletThrowsFileNotFoundException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);

                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

                var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                                                 dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                walletManager.LoadWallet("password", "testWallet");
            });
        }

        [Fact]
        public void RecoverWalletWithEqualInputAsExistingWalletRecoversWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            string password = "test";
            string passphrase = "this is my magic passphrase";
            string walletName = "mywallet";

            ChainIndexer chainIndexer = WalletTestsHelpers.PrepareChainWithBlock();
            (Wallet wallet, Mnemonic mnemonic) deletedWallet;
            {
                DataFolder oldDataFolder = CreateDataFolder(this, callingMethod: "RecoverWalletOnlyWithPasswordWalletRecoversWallet2");
                IWalletRepository oldWalletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, oldDataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());
                var oldWalletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                    oldDataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), oldWalletRepository);

                oldWalletManager.Start();

                // Create the wallet.
                deletedWallet = oldWalletManager.CreateWallet(password, walletName, passphrase);
            }

            // create a fresh manager.
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // Try to recover it.
            int? lastBlockSyncedHeight = (int)deletedWallet.wallet.AccountsRoot.First().LastBlockSyncedHeight;
            Wallet recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), passphrase,
                (lastBlockSyncedHeight == null) ? null : chainIndexer.GetHeader((int)lastBlockSyncedHeight));

            Wallet expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (int i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                AccountRoot recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                AccountRoot expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Single(recoveredAccountRoot.Accounts);
                Assert.Single(expectedAccountRoot.Accounts);

                for (int j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    HdAccount expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    HdAccount recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (int k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        HdAddress recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Empty(expectedAddress.Transactions);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (int l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        HdAddress recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Empty(expectedAddress.Transactions);
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

            string password = "test";
            string walletName = "mywallet";

            ChainIndexer chainIndexer = WalletTestsHelpers.PrepareChainWithBlock();
            (Wallet wallet, Mnemonic mnemonic) deletedWallet;
            {
                DataFolder oldDataFolder = CreateDataFolder(this, callingMethod: "RecoverWalletOnlyWithPasswordWalletRecoversWallet2");
                IWalletRepository oldWalletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, oldDataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());
                var oldWalletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                    oldDataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), oldWalletRepository);

                oldWalletManager.Start();

                // Create the wallet.
                deletedWallet = oldWalletManager.CreateWallet(password, walletName, password);
            }

            // create a fresh manager.
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // try to recover it.
            int? lastBlockSyncedHeight = (int)deletedWallet.wallet.AccountsRoot.First().LastBlockSyncedHeight;
            Wallet recoveredWallet = walletManager.RecoverWallet(password, walletName, deletedWallet.mnemonic.ToString(), DateTime.Now.AddDays(1), password,
                (lastBlockSyncedHeight == null) ? null : chainIndexer.GetHeader((int)lastBlockSyncedHeight));

            Wallet expectedWallet = deletedWallet.wallet;

            Assert.Equal(expectedWallet.Name, recoveredWallet.Name);
            Assert.Equal(expectedWallet.Network, recoveredWallet.Network);
            Assert.Equal(expectedWallet.EncryptedSeed, recoveredWallet.EncryptedSeed);
            Assert.Equal(expectedWallet.ChainCode, recoveredWallet.ChainCode);

            Assert.Equal(1, expectedWallet.AccountsRoot.Count);
            Assert.Equal(1, recoveredWallet.AccountsRoot.Count);

            for (int i = 0; i < recoveredWallet.AccountsRoot.Count; i++)
            {
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).CoinType, recoveredWallet.AccountsRoot.ElementAt(i).CoinType);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHeight);
                Assert.Equal(expectedWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash, recoveredWallet.AccountsRoot.ElementAt(i).LastBlockSyncedHash);

                AccountRoot recoveredAccountRoot = recoveredWallet.AccountsRoot.ElementAt(i);
                AccountRoot expectedAccountRoot = expectedWallet.AccountsRoot.ElementAt(i);

                Assert.Single(recoveredAccountRoot.Accounts);
                Assert.Single(expectedAccountRoot.Accounts);

                for (int j = 0; j < expectedAccountRoot.Accounts.Count; j++)
                {
                    HdAccount expectedAccount = expectedAccountRoot.Accounts.ElementAt(j);
                    HdAccount recoveredAccount = recoveredAccountRoot.Accounts.ElementAt(j);
                    Assert.Equal(expectedAccount.Name, recoveredAccount.Name);
                    Assert.Equal(expectedAccount.Index, recoveredAccount.Index);
                    Assert.Equal(expectedAccount.HdPath, recoveredAccount.HdPath);
                    Assert.Equal(expectedAccount.ExtendedPubKey, expectedAccount.ExtendedPubKey);

                    Assert.Equal(20, recoveredAccount.InternalAddresses.Count);

                    for (int k = 0; k < recoveredAccount.InternalAddresses.Count; k++)
                    {
                        HdAddress expectedAddress = expectedAccount.InternalAddresses.ElementAt(k);
                        HdAddress recoveredAddress = recoveredAccount.InternalAddresses.ElementAt(k);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Empty(expectedAddress.Transactions);
                        Assert.Equal(expectedAddress.Transactions.Count, recoveredAddress.Transactions.Count);
                    }

                    Assert.Equal(20, recoveredAccount.ExternalAddresses.Count);
                    for (int l = 0; l < recoveredAccount.ExternalAddresses.Count; l++)
                    {
                        HdAddress expectedAddress = expectedAccount.ExternalAddresses.ElementAt(l);
                        HdAddress recoveredAddress = recoveredAccount.ExternalAddresses.ElementAt(l);
                        Assert.Equal(expectedAddress.Index, recoveredAddress.Index);
                        Assert.Equal(expectedAddress.ScriptPubKey, recoveredAddress.ScriptPubKey);
                        Assert.Equal(expectedAddress.Address, recoveredAddress.Address);
                        Assert.Equal(expectedAddress.Pubkey, recoveredAddress.Pubkey);
                        Assert.Equal(expectedAddress.HdPath, recoveredAddress.HdPath);
                        Assert.Empty(expectedAddress.Transactions);
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
        public void GetUnusedAccountUsingNameForNonExistinAccountThrowsWalletException()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Assert.Throws<WalletException>(() =>
            {
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.GetUnusedAccount("nonexisting", "password");
            });
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);
            wallet.AddNewAccount("password", accountName: "unused");

            HdAccount result = walletManager.GetUnusedAccount("testWallet", "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletNameWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);
            HdAccount result = walletManager.GetUnusedAccount("testWallet", "password");
            Assert.Equal("account 0", result.Name);

            int addressBuffer = new WalletSettings(NodeSettings.Default(this.Network)).UnusedAddressesBuffer;
            Assert.Equal(addressBuffer, result.ExternalAddresses.Count);
            Assert.Equal(addressBuffer, result.InternalAddresses.Count);
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithExistingAccountReturnsUnusedAccountIfExistsOnWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);
            wallet.AddNewAccount("password", accountName: "unused");

            HdAccount result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("unused", result.Name);
            Assert.False(File.Exists(Path.Combine(dataFolder.WalletPath + $"/testWallet.wallet.json")));
        }

        [Fact]
        public void GetUnusedAccountUsingWalletWithoutUnusedAccountsCreatesAccountAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);

            HdAccount result = walletManager.GetUnusedAccount(wallet, "password");

            Assert.Equal("account 0", result.Name);
        }

        [Fact]
        public void CreateNewAccountGivenNoAccountsExistingInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);

            HdAccount result = wallet.AddNewAccount("password");

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);
            Assert.Equal($"account 0", result.Name);
            Assert.Equal(0, result.Index);
            Assert.Equal($"m/44'/0'/0'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(20, result.InternalAddresses.Count);
            Assert.Equal(20, result.ExternalAddresses.Count);
        }

        [Fact]
        public void CreateNewAccountGivenExistingAccountInWalletCreatesNewAccount()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);
            wallet.AddNewAccount("password", accountName: "unused");

            HdAccount result = wallet.AddNewAccount("password");

            Assert.Equal(2, wallet.AccountsRoot.ElementAt(0).Accounts.Count);
            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", this.Network), wallet.ChainCode);
            string expectedExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/1'")).Neuter().ToString(this.Network);
            Assert.Equal($"account 1", result.Name);
            Assert.Equal(1, result.Index);
            Assert.Equal($"m/44'/0'/1'", result.HdPath);
            Assert.Equal(expectedExtendedPubKey, result.ExtendedPubKey);
            Assert.Equal(20, result.InternalAddresses.Count);
            Assert.Equal(20, result.ExternalAddresses.Count);
        }

        [Fact]
        public void GetUnusedAddressUsingNameWithWalletWithoutAccountOfGivenNameThrowsException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                Wallet wallet = this.walletFixture.GenerateBlankWallet("testWallet", "password", walletRepository);

                HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("testWallet", "unexistingAccount"));
            });
        }

        [Fact]
        public void GetUnusedAddressUsingNameForNonExistinAccountThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                walletManager.GetUnusedAddress(new WalletAccountReference("nonexisting", "account"));
            });
        }

        [Fact]
        public void GetUnusedAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "myAccount");

            var pubKey1 = (new Key()).PubKey.Hash;
            var pubKey2 = (new Key()).PubKey.Hash;

            account.ExternalAddresses.Add(
                new HdAddress(
                    new List<TransactionData>
                    {
                        new TransactionData() {
                            Id = (uint256)0,
                            ScriptPubKey = pubKey1.ScriptPubKey
                        }
                    })
                {
                    Index = 0,
                    Address = "myUsedAddress",
                    ScriptPubKey = pubKey1.ScriptPubKey
                });

            account.ExternalAddresses.Add(new HdAddress
            {
                Index = 1,
                Address = "myUnusedAddress",
                ScriptPubKey = pubKey2.ScriptPubKey
            });

            HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            Assert.Equal(pubKey2.ScriptPubKey, result.ScriptPubKey);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);

            var account = wallet.AddNewAccount("password", 0, "myAccount");

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.NotNull(result);
        }

        [Fact]
        public void GetOrCreateChangeAddressWithWalletNotHavingUnusedAddressReturnsAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);

            var extKey = new ExtKey(Key.Parse(wallet.EncryptedSeed, "password", wallet.Network), wallet.ChainCode);
            string accountExtendedPubKey = extKey.Derive(new KeyPath($"m/44'/0'/0'")).Neuter().ToString(wallet.Network);

            HdAccount account = wallet.AddNewAccount(ExtPubKey.Parse(accountExtendedPubKey), 0, "myAccount");

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.NotNull(result.Address);
        }

        [Fact]
        public void GetUnusedAddressWithoutWalletHavingUnusedAddressCreatesAddressAndSavesWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount("password", accountName: "myAccount", addressCounts: (0, 0));

            // Allow manual addition of addresses.
            walletRepository.TestMode = true;

            Script myUsedScriptPubKey = new Key().PubKey.Hash.ScriptPubKey;

            account.ExternalAddresses.Add(new HdAddress(new List<TransactionData> {
                new TransactionData() {
                    ScriptPubKey = myUsedScriptPubKey,
                    Id = 0,
                    Index = 0
                }
            })
            {
                Index = 0,
                Address = "myUsedAddress",
                ScriptPubKey = myUsedScriptPubKey,
            });

            HdAddress result = walletManager.GetUnusedAddress(new WalletAccountReference("myWallet", "myAccount"));

            var keyPath = new KeyPath($"0/1");
            ExtPubKey extPubKey = ExtPubKey.Parse(account.ExtendedPubKey).Derive(keyPath);
            PubKey pubKey = extPubKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(wallet.Network);
            Assert.Equal(1, result.Index);
            Assert.Equal("m/44'/0'/0'/0/1", result.HdPath);
            Assert.Equal(address.ToString(), result.Address);
            Assert.Equal(pubKey.ScriptPubKey, result.Pubkey);
            Assert.Equal(address.ScriptPubKey, result.ScriptPubKey);
            Assert.Empty(result.Transactions);
        }

        [Fact]
        public void GetHistoryByNameWithExistingWalletReturnsAllAddressesWithTransactions()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                CreateDataFolder(this), new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "myAccount");

            account.ExternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 0, 0, "myUsedExternalAddress"));
            account.ExternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 0, 1, "myUnusedExternalAddress"));
            account.InternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 1, 0, "myUsedInternalAddress"));
            account.InternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 1, 1, "myUnusedInternalAddress"));

            var tx1 = this.Network.CreateTransaction();
            tx1.Outputs.Add(new TxOut(new Money(1.0m, MoneyUnit.BTC), account.ExternalAddresses.ElementAt(0).ScriptPubKey));
            walletRepository.ProcessTransaction("myWallet", tx1);

            var tx2 = this.Network.CreateTransaction();
            tx2.Outputs.Add(new TxOut(new Money(1.0m, MoneyUnit.BTC), account.InternalAddresses.ElementAt(0).ScriptPubKey));
            walletRepository.ProcessTransaction("myWallet", tx2);

            List<AccountHistory> result = walletManager.GetHistory("myWallet").ToList();

            Assert.NotEmpty(result);
            Assert.Single(result);
            AccountHistory accountHistory = result.ElementAt(0);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.NotEmpty(accountHistory.History);
            Assert.Equal(2, accountHistory.History.Count());

            FlatHistory historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal(account.ExternalAddresses.ElementAt(0).ScriptPubKey, historyAddress.Address.ScriptPubKey);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal(account.InternalAddresses.ElementAt(0).ScriptPubKey, historyAddress.Address.ScriptPubKey);
        }

        [Fact]
        public void GetHistoryByAccountWithExistingAccountReturnsAllAddressesWithTransactions()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "myAccount");

            account.ExternalAddresses.Add(WalletTestsHelpers.CreateAddressWithEmptyTransaction(account, 0, 0, "myUsedExternalAddress"));
            account.ExternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 0, 1, "myUnusedExternalAddress"));
            account.InternalAddresses.Add(WalletTestsHelpers.CreateAddressWithEmptyTransaction(account, 1, 0, "myUsedInternalAddress"));
            account.InternalAddresses.Add(WalletTestsHelpers.CreateAddressWithoutTransaction(account, 1, 1, "myUnusedInternalAddress"));

            AccountHistory accountHistory = walletManager.GetHistory(account);

            Assert.NotNull(accountHistory);
            Assert.NotNull(accountHistory.Account);
            Assert.Equal("myAccount", accountHistory.Account.Name);
            Assert.Equal(2, accountHistory.History.Count());

            FlatHistory historyAddress = accountHistory.History.ElementAt(0);
            Assert.Equal(account.ExternalAddresses.ElementAt(0).Address, historyAddress.Address.Address);
            historyAddress = accountHistory.History.ElementAt(1);
            Assert.Equal(account.InternalAddresses.ElementAt(0).Address, historyAddress.Address.Address);
        }

        [Fact]
        public void GetHistoryByAccountWithoutHavingAddressesWithTransactionsReturnsEmptyList()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount("password", accountName: "myAccount");

            AccountHistory result = walletManager.GetHistory(account);

            Assert.NotNull(result.Account);
            Assert.Equal("myAccount", result.Account.Name);
            Assert.Empty(result.History);
        }

        [Fact]
        public void GetHistoryByWalletNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);
                walletManager.GetHistory("noname");
            });
        }

        [Fact]
        public void GetWalletByNameWithExistingWalletReturnsWallet()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);

            Wallet result = walletManager.GetWallet("myWallet");

            Assert.Equal(wallet.EncryptedSeed, result.EncryptedSeed);
        }

        [Fact]
        public void GetWalletByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>((Action)(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);
                walletManager.GetWallet((string)"noname");
            }));
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletReturnsAccountsFromWallet()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 0", Index = 0 });
            wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount { Name = "Account 1", Index = 1 });

            IEnumerable<HdAccount> result = walletManager.GetAccounts("myWallet");

            Assert.Equal(2, result.Count());
            Assert.Equal("Account 0", result.ElementAt(0).Name);
            Assert.Equal("Account 1", result.ElementAt(1).Name);
        }

        [Fact]
        public void GetAccountsByNameWithExistingWalletMissingAccountsReturnsEmptyList()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);

            IEnumerable<HdAccount> result = walletManager.GetAccounts("myWallet");

            Assert.Empty(result);
        }

        [Fact]
        public void GetAccountsByNameWithoutExistingWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.GetAccounts("myWallet");
            });
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsReturnsChainTipHeight()
        {
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(this.Network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            int result = walletManager.LastBlockHeight();

            Assert.Equal(chain.Tip.Height, result);
        }

        [Fact]
        public void LastBlockHeightWithoutWalletsOfCoinTypeReturnsMinusOne()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;

            int result = walletManager.LastBlockHeight();

            Assert.Equal(-1, result);
        }

        [Fact]
        public void LastReceivedBlockHashWithoutWalletsReturnsChainTipHashBlock()
        {
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(this.Network.CreateTransaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            chain.SetTip(block.Header);

            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            uint256 result = walletManager.LastReceivedBlockInfo().Hash;

            Assert.Equal(chain.Tip.HashBlock, result);
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        //       This test case is highly questionable.
        //       If there are wallets present we should return the real wallet tip and not a made up value.
        /*
        [Fact]
        public void NoLastReceivedBlockHashInWalletReturnsChainTip()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(2, this.Network);
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            wallet.AccountsRoot.ElementAt(0).CoinType = CoinType.Stratis;

            uint256 result = walletManager.LastReceivedBlockInfo().Hash;
            Assert.Equal(chainIndexer.Tip.HashBlock, result);
        }
        */

        [Fact]
        public void GetSpendableTransactionsWithChainOfHeightZeroReturnsNoTransactions()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(0, this.Network);
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            var account = wallet.AddNewAccount((ExtPubKey)null);

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account.ExternalAddresses, this.Network, 1, 9, 10);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account.InternalAddresses, this.Network, 2, 9, 10);

            IEnumerable<UnspentOutputReference> result = walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);

            Assert.Empty(result);
        }

        // Make the transactions recorded against addresses appear in the database.
        private void ProcessAddressTransactions(Wallet wallet, ChainIndexer chainIndexer)
        {
            foreach (HdAccount account in wallet.GetAccounts())
            {
                foreach (HdAddress address in account.GetCombinedAddresses())
                {
                    foreach (TransactionData transactionData in address.Transactions)
                    {
                        Block block = chainIndexer.GetHeader((int)transactionData.BlockHeight).Block;

                        Transaction tx = this.Network.CreateTransaction();
                        tx.Outputs.Add(new TxOut(transactionData.Amount, transactionData.ScriptPubKey));

                        block.AddTransaction(tx);
                    }
                }
            }
        }

        /// <summary>
        /// If the block height of the transaction is x+ away from the current chain top transactions must be returned where x is higher or equal to the specified amount of confirmations.
        /// </summary>
        [Fact]
        public void GetSpendableTransactionsReturnsTransactionsGivenBlockHeight()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);

            HdAccount account0 = wallet.AddNewAccount((ExtPubKey)null, accountName: "First expectation");

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account0.ExternalAddresses, this.Network, 1, 9, 10);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account0.InternalAddresses, this.Network, 2, 9, 10);

            HdAccount account1 = wallet.AddNewAccount((ExtPubKey)null);

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account1.ExternalAddresses, KnownNetworks.StratisMain, 8, 9, 10);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account1.InternalAddresses, KnownNetworks.StratisMain, 8, 9, 10);

            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password", walletRepository);

            HdAccount account2 = wallet2.AddNewAccount((ExtPubKey)null);

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account2.ExternalAddresses, KnownNetworks.StratisMain, 1, 3, 5, 7, 9, 10);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account2.InternalAddresses, KnownNetworks.StratisMain, 2, 4, 6, 8, 9, 10);

            Wallet wallet3 = this.walletFixture.GenerateBlankWallet("myWallet3", "password", walletRepository);

            HdAccount account3 = wallet3.AddNewAccount((ExtPubKey)null, accountName: "Second expectation");

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account3.ExternalAddresses, this.Network, 5, 9, 11);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account3.InternalAddresses, this.Network, 6, 9, 11);

            UnspentOutputReference[] result = walletManager.GetSpendableTransactionsInWallet("myWallet3", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            UnspentOutputReference info = result[0];
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
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "First expectation");

            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account.ExternalAddresses, this.Network, 1, 9, 11);
            WalletTestsHelpers.CreateUnspentTransactionsOfBlockHeights(account.InternalAddresses, this.Network, 2, 9, 11);

            UnspentOutputReference[] result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1).ToArray();

            Assert.Equal(4, result.Count());
            UnspentOutputReference info = result[0];
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
                ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.GetSpendableTransactionsInWallet("myWallet", confirmations: 1);
            });
        }

        [Fact]
        public void GetSpendableTransactionsWithOnlySpentTransactionsReturnsEmptyList()
        {
            ChainIndexer chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(10, this.Network);
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "First expectation");

            foreach (HdAddress addr in WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 1, 9, 10))
                account.ExternalAddresses.Add(addr);
            foreach (HdAddress addr in WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 2, 9, 10))
                account.InternalAddresses.Add(addr);

            IEnumerable<UnspentOutputReference> result = walletManager.GetSpendableTransactionsInWallet("myWallet1", confirmations: 1);

            Assert.Empty(result);
        }

        [Fact]
        public void GetKeyForAddressWithoutWalletsThrowsWalletException()
        {
            Assert.Throws<WalletException>((Action)(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                Wallet wallet = walletManager.GetWallet((string)"mywallet");
                Key key = wallet.GetExtendedPrivateKeyForAddress("password", new HdAddress()).PrivateKey;
            }));
        }

        [Fact]
        public void GetKeyForAddressWithWalletReturnsAddressExtPrivateKey()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            (Wallet wallet, ExtKey key) = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount("password", accountName: "savings account", addressCounts: (1, 0));
            HdAddress address = account.GetFirstUnusedReceivingAddress();

            ISecret result = wallet.GetExtendedPrivateKeyForAddress("password", address);

            Assert.Equal(key.Derive(new KeyPath("m/44'/0'/0'/0/0")).GetWif(wallet.Network), result);
        }

        [Fact]
        public void GetKeyForAddressWitoutAddressOnWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var dataFolder = CreateDataFolder(this);
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                (Wallet wallet, ExtKey key) = WalletTestsHelpers.GenerateBlankWalletWithExtKey("myWallet", "password", walletRepository);

                var address = new HdAddress
                {
                    Index = 0,
                    HdPath = "m/44'/0'/0'/0/0",
                };

                wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
                {
                    Index = 0,
                    Name = "savings account"
                });

                wallet.GetExtendedPrivateKeyForAddress("password", address);
            });
        }

        [Fact]
        public void ProcessTransactionWithValidTransactionLoadsTransactionsIntoWalletIfMatching()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account1");

            account.ExternalAddresses.Add(spendingAddress);
            account.ExternalAddresses.Add(destinationAddress);
            account.InternalAddresses.Add(changeAddress);

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            walletManager.ProcessBlock(chainInfo.block);
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions);
            TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithEmptyScriptInTransactionDoesNotAddTransactionToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account1");

            account.ExternalAddresses.Add(spendingAddress);
            account.ExternalAddresses.Add(destinationAddress);
            account.InternalAddresses.Add(changeAddress);

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            transaction.Outputs.ElementAt(1).Value = Money.Zero;
            transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

            walletManager.ProcessBlock(chainInfo.block);
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

            Assert.Empty(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationToChangeAddressDoesNotAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/1");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            var destinationChangeAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/1/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account");

            account.ExternalAddresses.Add(spendingAddress);
            account.InternalAddresses.Add(changeAddress);
            account.InternalAddresses.Add(destinationChangeAddress);

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            walletManager.ProcessBlock(chainInfo.block);
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);
            Assert.Equal(1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Null(destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithDestinationAsMultisigAddTransactionAsPayment()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount("password", accountName: "account1", addressCounts: (1, 1));
            HdAddress spendingAddress = account.ExternalAddresses.ElementAt(0);
            HdAddress changeAddress = account.InternalAddresses.ElementAt(0);

            // Generate a spendable transaction.
            (uint256 blockhash, Block block) = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            walletManager.ProcessBlock(block);

            // Setup a payment to yourself.
            Script scriptToHash = new PayToScriptHashTemplate().GenerateScriptPubKey(new Key().PubKey.ScriptPubKey);
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, scriptToHash, changeAddress, new Money(7500), new Money(5000));
            walletManager.ProcessTransaction(transaction);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
        }

        [Fact]
        public void ProcessTransactionWithBlockHeightSetsBlockHeightOnTransactionData()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);

            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account1");

            account.ExternalAddresses.Add(spendingAddress);
            account.ExternalAddresses.Add(destinationAddress);
            account.InternalAddresses.Add(changeAddress);

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chain, transaction);

            int blockHeight = chain.GetHeader(block.GetHash()).Height;
            //walletManager.ProcessTransaction(transaction, blockHeight);

            walletManager.ProcessBlock(chainInfo.block);
            walletManager.ProcessBlock(block);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(blockHeight - 1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions);
            TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
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

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);
            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account1");

            account.ExternalAddresses.Add(spendingAddress);
            account.ExternalAddresses.Add(destinationAddress);
            account.InternalAddresses.Add(changeAddress);

            // setup a payment to yourself
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chain, transaction);

            walletManager.ProcessBlock(chainInfo.block);
            walletManager.ProcessBlock(block);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
            Assert.Equal(chainInfo.block.GetHash(), spentAddressResult.Transactions.ElementAt(0).BlockHash);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions);
            TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
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

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
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

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
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

        //        var walletManager = new WalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain, NodeSettings.Default(this.Network), new WalletSettings(NodeSettings.Default(this.Network)),
        //            dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default);
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

        private void AdvanceWalletTipToChainTip(IWalletRepository walletRepository, ChainIndexer concurrentchain)
        {
            var block0 = this.Network.CreateBlock();
            block0.Header.HashPrevBlock = null;
            var header0 = new ChainedHeader(block0.Header, this.Network.GenesisHash, 0);
            walletRepository.ProcessBlock(block0, header0);
            for (int i = 1; i <= concurrentchain.Height; i++)
            {
                ChainedHeader header = concurrentchain.GetHeader(i);
                walletRepository.ProcessBlock(this.Network.CreateBlock(), header);
            }
        }

        // TODO: The new wallet implementation is too strict to allow its tip to be set to a fictitious block height.
        //       The included block chain data is only up to block 3 while the test creates blocks up to height 5. Rework this test.
        /*
        [Fact]
        public void RemoveBlocksRemovesTransactionsWithHigherBlockHeightAndUpdatesLastSyncedBlockHeight()
        {
            uint256 trxId = uint256.Parse("21e74d1daed6dec93d58396a3406803c5fc8d220b59f4b4dd185cab5f7a9a22e");
            int trxCount = 0;
            var concurrentchain = new ChainIndexer(this.Network);
            ChainedHeader chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, chainedHeader, concurrentchain).ChainedHeader;
            chainedHeader = WalletTestsHelpers.AppendBlock(this.Network, chainedHeader, concurrentchain).ChainedHeader;

            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, concurrentchain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "First account");

            var extAddresses = new List<HdAddress>();
            foreach (HdAddress addr in WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 1, 2, 3, 4, 5))
                extAddresses.Add(addr);
            var intAddresses = new List<HdAddress>();
            foreach (HdAddress addr in WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(this.Network, 1, 2, 3, 4, 5))
                intAddresses.Add(addr);

            //walletManager.Stop();

            // TODO: Having blocks beyond the chain tip is not valid. Rework this test.

            // reorg at block 3

            // Trx at block 0 is not spent
            extAddresses.ElementAt(0).Transactions.First().Id = trxId >> trxCount++; ;
            extAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;
            intAddresses.ElementAt(0).Transactions.First().Id = trxId >> trxCount++;
            intAddresses.ElementAt(0).Transactions.First().SpendingDetails = null;

            // Trx at block 2 is spent in block 3, after reorg it will not be spendable.
            extAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++;
            extAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;
            intAddresses.ElementAt(1).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++;
            intAddresses.ElementAt(1).Transactions.First().SpendingDetails.BlockHeight = 3;

            // Trx at block 3 is spent at block 5, after reorg it will be spendable.
            extAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++; ;
            extAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;
            intAddresses.ElementAt(2).Transactions.First().SpendingDetails.TransactionId = trxId >> trxCount++; ;
            intAddresses.ElementAt(2).Transactions.First().SpendingDetails.BlockHeight = 5;

            foreach (HdAddress address in extAddresses)
                account.ExternalAddresses.Add(address);
            foreach (HdAddress address in intAddresses)
                account.InternalAddresses.Add(address);

            // Ensure that the wallet tip reflects all the blocks that have been processed.
            AdvanceWalletTipToChainTip(walletRepository, concurrentchain);

            // Now rewind the wallet by 2 blocks.
            walletManager.RemoveBlocks(chainedHeader);

            // Refresh the wallet fields.
            wallet = walletManager.GetWallet(wallet.Name);

            Assert.Equal(chainedHeader.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedHeader.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedHeader.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedHeader.HashBlock, walletManager.WalletTipHash);

            Assert.Equal(6, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).Count());
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.BlockHeight <= chainedHeader.Height));
            Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).All(r => r.SpendingDetails == null || r.SpendingDetails.BlockHeight <= chainedHeader.Height));
            Assert.Equal(4, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).Count(t => t.SpendingDetails == null));
        }
        */

        [Fact]
        public void ProcessBlockWithoutWalletsSetsWalletTipToBlockHash()
        {
            var concurrentchain = new ChainIndexer(this.Network);
            (ChainedHeader ChainedHeader, Block Block) blockResult = WalletTestsHelpers.AppendBlock(this.Network, null, concurrentchain);

            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, concurrentchain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            walletManager.ProcessBlock(blockResult.Block, blockResult.ChainedHeader);

            Assert.Equal(blockResult.ChainedHeader.HashBlock, walletManager.WalletTipHash);
        }

        [Fact]
        public void ProcessBlockWithWalletsProcessesTransactionsOfBlockToWallet()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var chain = new ChainIndexer(this.Network);
            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);

            (ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
            (PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
            (PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
            (PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

            var spendingAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/0/0",
                Address = spendingKeys.Address.ToString(),
                Pubkey = spendingKeys.PubKey.ScriptPubKey,
                ScriptPubKey = spendingKeys.Address.ScriptPubKey
            };

            //Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, this.Network, spendingAddress);

            var destinationAddress = new HdAddress
            {
                Index = 1,
                HdPath = $"m/44'/0'/0'/0/1",
                Address = destinationKeys.Address.ToString(),
                Pubkey = destinationKeys.PubKey.ScriptPubKey,
                ScriptPubKey = destinationKeys.Address.ScriptPubKey
            };

            var changeAddress = new HdAddress
            {
                Index = 0,
                HdPath = $"m/44'/0'/0'/1/0",
                Address = changeKeys.Address.ToString(),
                Pubkey = changeKeys.PubKey.ScriptPubKey,
                ScriptPubKey = changeKeys.Address.ScriptPubKey
            };

            TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chain, chainInfo);
            spendingAddress.Transactions.Add(spendingTransaction);

            // setup a payment to yourself in a new block.
            Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
            Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chain, transaction);

            var account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account1");

            account.ExternalAddresses.Add(spendingAddress);
            account.ExternalAddresses.Add(destinationAddress);
            account.InternalAddresses.Add(changeAddress);

            walletManager.ProcessBlock(chainInfo.block);

            ChainedHeader chainedBlock = chain.GetHeader(block.GetHash());

            walletManager.ProcessBlock(block);

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions);
            TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

            // Referesh wallet fields.
            wallet = walletManager.GetWallet(wallet.Name);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        //       It is probably better that a block that is significantly ahead of any given wallet simply does not get processed by that particular wallet...
        /*
        [Fact]
        public void ProcessBlockWithWalletTipBlockNotOnChainYetThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

                var chain = new ChainIndexer(this.Network);
                (ChainedHeader ChainedHeader, Block Block) chainResult = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);

                //walletManager.WalletTipHash = new uint256(15012522521);

                walletManager.ProcessBlock(chainResult.Block, chainResult.ChainedHeader);
            });
        }
        */

        // TODO: Investigate the relevance of this test and remove it or fix it.
        //       It is probably better that a block that is significantly ahead of any given wallet simply does not get processed by that particular wallet...
        /*
        [Fact]
        public void ProcessBlockWithBlockAheadOfWalletThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                DataFolder dataFolder = CreateDataFolder(this);
                Directory.CreateDirectory(dataFolder.WalletPath);

                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

                var chain = new ChainIndexer(this.Network);
                (ChainedHeader ChainedHeader, Block Block) chainResult = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain);
                (ChainedHeader ChainedHeader, Block Block) chainResult2 = WalletTestsHelpers.AppendBlock(this.Network, chainResult.ChainedHeader, chain);

                var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);


                walletManager.Start();

                Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);

                //walletManager.WalletTipHash = wallet.Network.GetGenesis().Header.GetHash();

                walletManager.ProcessBlock(chainResult2.Block, chainResult2.ChainedHeader);
            });
        }
        */

        [Fact]
        public void CheckWalletBalanceEstimationWithConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 2);

            HdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two unconfirmed transactions
            for (int i = 0; i < 2; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i), Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i + 2), Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetBalances().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetBalances().UnConfirmedAmount);
        }

        [Fact]
        public void GetAccountBalancesReturnsCorrectAccountBalances()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            // Initialize chain object.
            var chain = new ChainIndexer(KnownNetworks.StratisMain);
            uint nonce = RandomUtils.GetUInt32();
            var block = this.Network.CreateBlock();
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = nonce;
            block.Header.BlockTime = DateTimeOffset.Now;
            chain.SetTip(block.Header);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = WalletTestsHelpers.CreateWallet("myWallet", walletRepository);
            HdAccount account = wallet.AddNewAccount((ExtPubKey)null, accountName: "account 1");
            HdAccount account2 = wallet.AddNewAccount((ExtPubKey)null, accountName: "account 2");

            HdAddress accountAddress1 = WalletTestsHelpers.CreateAddress(false, account);
            account.ExternalAddresses.Add(accountAddress1);
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1));

            HdAddress accountAddress2 = WalletTestsHelpers.CreateAddress(true, account);
            account.InternalAddresses.Add(accountAddress2);
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 2));

            HdAddress account2Address1 = WalletTestsHelpers.CreateAddress(false, account2);
            account2.ExternalAddresses.Add(account2Address1);
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(5), new Money(74000), null));
            account2Address1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(6), new Money(18700), 3));

            HdAddress account2Address2 = WalletTestsHelpers.CreateAddress(true, account2);
            account2.InternalAddresses.Add(account2Address2);
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(7), new Money(65000), null));
            account2Address2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(8), new Money(89300), 4));

            // Set the tip for maturity calculation.
            WalletTestsHelpers.AppendBlock(this.Network, null, chain);
            WalletTestsHelpers.AppendBlock(this.Network, null, chain);
            WalletTestsHelpers.AppendBlock(this.Network, null, chain);
            WalletTestsHelpers.AppendBlock(this.Network, null, chain);

            // Act.
            IEnumerable<AccountBalance> balances = walletManager.GetBalances(wallet.Name);

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

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 20);

            HdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two confirmed transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 1, Index = i, Amount = 10, BlockHeight = 10, BlockHash = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10, BlockHeight = 10, BlockHash = 10 });
            }

            Assert.Equal(40, firstAccount.GetBalances().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 20);

            HdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 1, Index = i, Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            Assert.Equal(0, firstAccount.GetBalances().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 5);

            HdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i), Amount = 10, BlockHeight = 10, BlockHash = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i), Amount = 10, BlockHeight = 10, BlockHash = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i), Amount = 10, BlockHeight = 10, BlockHash = 10, });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = new uint256((ulong)i), Amount = 10, BlockHeight = 10, BlockHash = 10 });
            }

            Assert.Equal(40, firstAccount.GetBalances().ConfirmedAmount);
            Assert.Equal(0, firstAccount.GetBalances().UnConfirmedAmount);
        }

        [Fact]
        public void CheckWalletBalanceEstimationWithSpentAndUnConfirmedTransactions()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // generate 3 wallet with 2 accounts containing 20 external and 20 internal addresses each.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 20);

            HdAccount firstAccount = walletManager.Wallets.First().AccountsRoot.Single().Accounts.First();

            // add two spent transactions
            for (int i = 1; i < 3; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10, BlockHeight = 10, SpendingDetails = new SpendingDetails() });
            }

            for (int i = 3; i < 5; i++)
            {
                firstAccount.InternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10 });
                firstAccount.ExternalAddresses.ElementAt(i).Transactions.Add(new TransactionData { Id = 0, Index = i, Amount = 10 });
            }

            Assert.Equal(0, firstAccount.GetBalances().ConfirmedAmount);
            Assert.Equal(40, firstAccount.GetBalances().UnConfirmedAmount);
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        /*
        [Fact]
        public void SaveToFileWithoutWalletParameterSavesAllWalletsOnManagerToDisk()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test", walletRepository);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

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

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test", walletRepository);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

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
        */

        [Fact]
        public void GetWalletFileExtensionReturnsWalletExtension()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            string result = walletManager.GetWalletFileExtension();

            Assert.Equal("wallet.json", result);
        }

        [Fact]
        public void GetWalletsReturnsLoadedWalletNames()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test", walletRepository);

            string[] result = walletManager.GetWalletsNames().OrderBy(w => w).ToArray();

            Assert.Equal(2, result.Count());
            Assert.Equal("wallet1", result[0]);
            Assert.Equal("wallet2", result[1]);
        }

        [Fact]
        public void GetWalletsWithoutLoadedWalletsReturnsEmptyList()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            IOrderedEnumerable<string> result = walletManager.GetWalletsNames().OrderBy(w => w);

            Assert.Empty(result);
        }

        [Fact]
        public void CreateBip44PathWithChangeAddressReturnsPath()
        {
            string result = HdOperations.CreateHdPath((int)CoinType.Stratis, 4, true, 3);

            Assert.Equal("m/44'/105'/4'/1/3", result);
        }

        [Fact]
        public void CreateBip44PathWithoutChangeAddressReturnsPath()
        {
            string result = HdOperations.CreateHdPath((int)CoinType.Stratis, 4, false, 3);

            Assert.Equal("m/44'/105'/4'/0/3", result);
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        /*
        [Fact]
        public void StopSavesWallets()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            Wallet wallet = this.walletFixture.GenerateBlankWallet("wallet1", "test", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("wallet2", "test", walletRepository);

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

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
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password", walletRepository);

            var chain = new ChainIndexer(wallet.Network);
            ChainedHeader chainedBlock = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);
            //walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(chainedBlock);

            Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
            foreach (Wallet w in walletManager.Wallets)
            {
                Assert.Equal(chainedBlock.GetLocator().Blocks, w.BlockLocator);
                Assert.Equal(chainedBlock.Height, w.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
                Assert.Equal(chainedBlock.HashBlock, w.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            }
        }

        [Fact]
        public void UpdateLastBlockSyncedHeightWithWalletAndChainedBlockUpdatesGivenWallet()
        {
            var dataFolder = CreateDataFolder(this);
            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            Wallet wallet2 = this.walletFixture.GenerateBlankWallet("myWallet2", "password", walletRepository);

            var chain = new ChainIndexer(wallet.Network);
            ChainedHeader chainedBlock = WalletTestsHelpers.AppendBlock(this.Network, chain.Genesis, chain).ChainedHeader;

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);
            //walletManager.WalletTipHash = new uint256(125125125);

            walletManager.UpdateLastBlockSyncedHeight(wallet, chainedBlock);

            Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
            Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
            Assert.NotEqual(chainedBlock.HashBlock, walletManager.WalletTipHash);

            Assert.NotEqual(chainedBlock.GetLocator().Blocks, wallet2.BlockLocator);
            Assert.NotEqual(chainedBlock.Height, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
            Assert.NotEqual(chainedBlock.HashBlock, wallet2.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
        }
        */

        [Fact]
        public void RemoveAllTransactionsInWalletReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 0);

            HdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            for (int i = 0; i < 6; i++)
            {
                var extAddress = WalletTestsHelpers.CreateAddress(false, firstAccount, i);
                extAddress.Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
                firstAccount.ExternalAddresses.Add(extAddress);
                var intAddress = WalletTestsHelpers.CreateAddress(true, firstAccount, i);
                intAddress.Transactions.Add(new TransactionData { Amount = 10, Id = trxId >> counter++ });
                firstAccount.InternalAddresses.Add(intAddress);
            }

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(12, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Equal(12, result.Count);
        }

        [Fact]
        public void RemoveAllTransactionsWhenNoTransactionsArePresentReturnsEmptyList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // Generate a wallet with an account and no transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 20);

            HdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(0, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveAllTransactions("wallet1");

            // Assert.
            Assert.Empty(firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions));
            Assert.Empty(result);
        }

        [Fact]
        public void RemoveTransactionsByIdsWhenTransactionsAreUnconfirmedReturnsRemovedTransactionsList()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 0);

            HdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            var extAddresses = new List<HdAddress>();
            extAddresses.Add(WalletTestsHelpers.CreateAddress(false, firstAccount, 0));
            extAddresses.Add(WalletTestsHelpers.CreateAddress(false, firstAccount, 1));

            var intAddresses = new List<HdAddress>();
            intAddresses.Add(WalletTestsHelpers.CreateAddress(true, firstAccount, 0));
            intAddresses.Add(WalletTestsHelpers.CreateAddress(true, firstAccount, 1));

            var trxUnconfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++ };
            var trxUnconfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++ };
            var trxConfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50000 };
            var trxConfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50001 };

            extAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            extAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            intAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed2);
            intAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            foreach (HdAddress address in extAddresses)
                firstAccount.ExternalAddresses.Add(address);

            foreach (HdAddress address in intAddresses)
                firstAccount.InternalAddresses.Add(address);

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(4, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveTransactionsByIds("wallet1", new[] { trxUnconfirmed1.Id, trxUnconfirmed2.Id, trxConfirmed1.Id, trxConfirmed2.Id });

            // Assert.
            List<TransactionData> remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count());
            Assert.Equal(2, result.Count);
            Assert.Contains((trxUnconfirmed1.Id, trxConfirmed1.CreationTime), result);
            Assert.Contains((trxUnconfirmed2.Id, trxConfirmed2.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);
            Assert.DoesNotContain(trxUnconfirmed2, remainingTrxs);
        }

        [Fact]
        public void ConfirmedTransactionsShouldWipeOutUnconfirmedWithSameInputs()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chain, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, walletFeePolicy.Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password", walletRepository);
            HdAccount account = wallet.AddNewAccount("password", accountName: "account1", addressCounts: (2, 1));
            HdAddress spendingAddress = account.ExternalAddresses.ElementAt(0);
            HdAddress destinationAddress = account.ExternalAddresses.ElementAt(1);
            HdAddress changeAddress = account.InternalAddresses.ElementAt(0);

            // Generate a spendable transaction.
            (uint256 blockhash, Block block) = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, this.Network, spendingAddress);
            walletManager.ProcessBlock(block, chain.GetHeader(1));

            // Refresh wallet fields.
            wallet = walletManager.GetWallet(wallet.Name);

            // Set up a transaction that will arrive through the mempool.
            Transaction transaction1 = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationAddress.ScriptPubKey, changeAddress, new Money(7500), new Money(5000));

            // Set up a different transaction spending the same inputs, with a higher fee, that will arrive in a block.
            Transaction transaction2 = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationAddress.ScriptPubKey, changeAddress, new Money(7500), new Money(10_000));

            Assert.Equal(transaction1.Inputs[0].PrevOut, transaction2.Inputs[0].PrevOut);
            Assert.NotEqual(transaction1.GetHash(), transaction2.GetHash());

            // First add transaction 1 via mempool.
            walletManager.ProcessTransaction(transaction1);

            // The first transaction should be present in the wallet.
            Assert.True(walletRepository.GetWalletAddressLookup(wallet.Name).Contains(destinationAddress.ScriptPubKey, out AddressIdentifier addressIdentifier));
            Assert.Contains(walletRepository.GetAllTransactions(addressIdentifier), t => t.Id == transaction1.GetHash());

            // Now add transaction 2 via block.
            Block block2 = this.Network.CreateBlock();
            block2.AddTransaction(transaction2);
            block2.Header.HashPrevBlock = chain.Tip.HashBlock;

            var header2 = new ChainedHeader(block2.Header, block2.Header.GetHash(), chain.Tip);

            walletManager.ProcessBlock(block2, header2);

            // The first transaction should no longer be present in the wallet.
            Assert.DoesNotContain(walletRepository.GetAllTransactions(addressIdentifier), t => t.Id == transaction1.GetHash());

            // The second transaction should be present.
            Assert.Contains(walletRepository.GetAllTransactions(addressIdentifier), t => t.Id == transaction2.GetHash());
        }

        [Fact]
        public void RemoveTransactionsByIdsAlsoRemovesUnconfirmedSpendingDetailsTransactions()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, new ScriptAddressReader());
            var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, new Mock<ChainIndexer>().Object, new WalletSettings(NodeSettings.Default(this.Network)),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

            walletManager.Start();

            // Generate a wallet with an account and a few transactions.
            Wallet wallet = WalletTestsHelpers.CreateWallet("wallet1", walletRepository);
            WalletTestsHelpers.AddAddressesToWallet(wallet, 0);

            HdAccount firstAccount = wallet.AccountsRoot.Single().Accounts.First();

            // Add two unconfirmed transactions.
            uint256 trxId = uint256.Parse("d6043add63ec364fcb591cf209285d8e60f1cc06186d4dcbce496cdbb4303400");
            int counter = 0;

            // Confirmed transaction with confirmed spending.
            var confirmedSpendingDetails = new SpendingDetails { TransactionId = trxId >> counter++, BlockHeight = 500002 };
            var trxConfirmed1 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50000, SpendingDetails = confirmedSpendingDetails };

            // Confirmed transaction with unconfirmed spending.
            uint256 unconfirmedTransactionId = trxId >> counter++;
            var unconfirmedSpendingDetails1 = new SpendingDetails { TransactionId = unconfirmedTransactionId };
            var trxConfirmed2 = new TransactionData { Amount = 10, Id = trxId >> counter++, BlockHeight = 50001, SpendingDetails = unconfirmedSpendingDetails1 };

            // Unconfirmed transaction.
            var trxUnconfirmed1 = new TransactionData { Amount = 10, Id = unconfirmedTransactionId };

            var extAddresses = new List<HdAddress>();
            extAddresses.Add(WalletTestsHelpers.CreateAddress(false, firstAccount, 0));
            extAddresses.Add(WalletTestsHelpers.CreateAddress(false, firstAccount, 1));

            var intAddresses = new List<HdAddress>();
            intAddresses.Add(WalletTestsHelpers.CreateAddress(true, firstAccount, 0));
            intAddresses.Add(WalletTestsHelpers.CreateAddress(true, firstAccount, 1));

            extAddresses.ElementAt(0).Transactions.Add(trxUnconfirmed1);
            extAddresses.ElementAt(1).Transactions.Add(trxConfirmed1);
            intAddresses.ElementAt(1).Transactions.Add(trxConfirmed2);

            foreach (HdAddress address in extAddresses)
                firstAccount.ExternalAddresses.Add(address);

            foreach (HdAddress address in intAddresses)
                firstAccount.InternalAddresses.Add(address);

            int transactionCount = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).Count();
            Assert.Equal(3, transactionCount);

            // Act.
            HashSet<(uint256, DateTimeOffset)> result = walletManager.RemoveTransactionsByIds("wallet1", new[]
            {
                trxConfirmed1.Id, // Shouldn't be removed.
                unconfirmedTransactionId, // A transaction + a spending transaction should be removed.
                trxConfirmed2.Id, // Shouldn't be removed.
                confirmedSpendingDetails.TransactionId, // Shouldn't be removed.
            });


            // Assert.
            List<TransactionData> remainingTrxs = firstAccount.GetCombinedAddresses().SelectMany(a => a.Transactions).ToList();
            Assert.Equal(2, remainingTrxs.Count);
            Assert.Single(result);
            Assert.Contains((unconfirmedTransactionId, trxUnconfirmed1.CreationTime), result);
            Assert.DoesNotContain(trxUnconfirmed1, remainingTrxs);

            // Refresh memory structure.
            Assert.Equal(trxConfirmed2.Id, firstAccount.InternalAddresses.ElementAt(1).Transactions.ElementAt(0).Id);
            trxConfirmed2 = firstAccount.InternalAddresses.ElementAt(1).Transactions.ElementAt(0);

            Assert.Null(trxConfirmed2.SpendingDetails);
        }

        // TODO: Investigate the relevance of this test and remove it or fix it.
        /*
        [Fact]
        public void Start_takes_account_of_address_buffer_even_for_existing_wallets()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            WalletManager walletManager = this.CreateWalletManager(dataFolder, this.Network);

            walletManager.Start();

            (Wallet wallet, _) = walletManager.CreateWallet("test", "mywallet", passphrase: new Mnemonic(Wordlist.English, WordCount.Eighteen).ToString());

            // Default of 20 addresses becuause walletaddressbuffer not set
            HdAccount hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();
            Assert.Equal(20, hdAccount.ExternalAddresses.Count);
            Assert.Equal(20, hdAccount.InternalAddresses.Count);

            // Restart with walletaddressbuffer set
            walletManager = this.CreateWalletManager(dataFolder, this.Network, "-walletaddressbuffer=30");
            walletManager.Start();

            wallet.WalletManager = walletManager;

            // Addresses populated to fill the buffer set
            hdAccount = walletManager.Wallets.Single().AccountsRoot.Single().Accounts.Single();
            Assert.Equal(30, hdAccount.ExternalAddresses.Count);
            Assert.Equal(30, hdAccount.InternalAddresses.Count);
        }
        */

        [Fact]
        public void Recover_via_xpubkey_can_recover_wallet_without_mnemonic()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            var walletManager = this.CreateWalletManager(dataFolder, this.Network);

            walletManager.Start();

            walletManager.RecoverWallet("testWallet", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            var wallet = walletManager.LoadWallet("password", "testWallet");

            wallet.IsExtPubKeyWallet.Should().BeTrue();
            wallet.EncryptedSeed.Should().BeNull();
            wallet.ChainCode.Should().BeNull();

            wallet.AccountsRoot.SelectMany(x => x.Accounts).Single().ExtendedPubKey
                .Should().Be(stratisAccount0ExtPubKey);
        }

        [Fact]
        public void AddNewAccount_via_xpubkey_prevents_adding_an_account_as_an_existing_account_index()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            const string stratisAccount1ExtPubKey = "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1";
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain);

            walletManager.Start();

            var wallet = walletManager.RecoverWallet("wallet1", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            try
            {
                wallet.AddNewAccount(ExtPubKey.Parse(stratisAccount1ExtPubKey), 0, accountCreationTime: DateTime.Now.AddHours(-2));

                Assert.True(false, "should have thrown exception but didn't.");
            }
            catch (WalletException e)
            {
                Assert.Equal("There is already an account in this wallet with index: " + 0, e.Message);
            }
        }

        [Fact]
        public void AddNewAccount_via_xpubkey_prevents_adding_the_same_xpub_key_as_different_account()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            const string stratisAccount0ExtPubKey = "xpub661MyMwAqRbcEgnsMFfhjdrwR52TgicebTrbnttywb9zn3orkrzn6MHJrgBmKrd7MNtS6LAim44a6V2gizt3jYVPHGYq1MzAN849WEyoedJ";
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain);

            walletManager.Start();

            var wallet = walletManager.RecoverWallet("wallet1", ExtPubKey.Parse(stratisAccount0ExtPubKey), 0, DateTime.Now.AddHours(-2));

            var addNewAccount = new Action(() => wallet.AddNewAccount(ExtPubKey.Parse(stratisAccount0ExtPubKey), 1, accountCreationTime: DateTime.Now.AddHours(-2)));

            addNewAccount.Should().Throw<WalletException>()
                .WithMessage("There is already an account in this wallet with this xpubkey: " + stratisAccount0ExtPubKey);
        }

        [Fact]
        public void CreateDefaultWalletAndVerifyTheDefaultPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            // Load the default wallet.
            var wallet = walletManager.LoadWallet("default", "default");

            Assert.NotNull(wallet);
        }

        [Fact]
        public void CreateDefaultWalletAndVerifyWrongPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default", "-defaultwalletpassword=default2");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            Assert.Throws<System.Security.SecurityException>(() =>
            {
                // Attempt to load the default wallet with wrong password.
                var wallet = walletManager.LoadWallet("default", "default");
            });
        }

        /// <summary>
        /// Test that first creates an unlocked default wallet, retrieves the extkey to verify it is actually unlocked. Lock the wallet, verify
        /// it is not possible to get extkey. Unlock manually, and verify that returned key is same as before.
        /// </summary>
        [Fact]
        public void CreateDefaultWalletAndVerifyUnlockAndLocking()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default", "-unlockdefaultwallet");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var wallet = walletManager.GetWallet("default");

            HdAccount account = walletManager.GetAccounts("default").Single();
            var reference = new WalletAccountReference("default", account.Name);

            var extKey1 = walletManager.GetExtKey(reference);
            walletManager.LockWallet("default");

            Assert.Throws<System.Security.SecurityException>(() =>
            {
                walletManager.GetExtKey(reference);
            });

            walletManager.UnlockWallet("default", "default", 10);

            var extKey2 = walletManager.GetExtKey(reference);

            Assert.Equal(extKey1.ToString(wallet.Network), extKey2.ToString(wallet.Network));
        }

        private WalletManager CreateWalletManager(DataFolder dataFolder, Network network, params string[] cmdLineArgs)
        {
            var nodeSettings = new NodeSettings(KnownNetworks.RegTest, ProtocolVersion.PROTOCOL_VERSION, network.Name, cmdLineArgs);
            var walletSettings = new WalletSettings(nodeSettings);

            IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, network, DateTimeProvider.Default, new ScriptAddressReader());

            return new WalletManager(this.LoggerFactory.Object, network, new ChainIndexer(network),
                walletSettings, dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);
        }

        private (Mnemonic mnemonic, Wallet wallet) CreateWalletOnDiskAndDeleteWallet(DataFolder dataFolder, string password, string passphrase, string walletName, ChainIndexer chainIndexer)
        {
            Mnemonic mnemonic;
            Wallet wallet;
            {
                IWalletRepository walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, KnownNetworks.StratisMain, DateTimeProvider.Default, new ScriptAddressReader());
                var walletManager = new WalletManager(this.LoggerFactory.Object, KnownNetworks.StratisMain, chainIndexer, new WalletSettings(NodeSettings.Default(this.Network)),
                    dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncProvider>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader(), walletRepository);

                walletManager.Start();

                // create the wallet
                (wallet, mnemonic) = walletManager.CreateWallet(password, walletName, passphrase);

                walletManager.Stop();
            }

            Directory.Delete(Path.Combine(dataFolder.WalletPath, "SQLiteWalletRepository"), true);

            return (mnemonic, wallet);
        }
    }
}