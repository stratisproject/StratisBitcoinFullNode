using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Tests
{
    public class WatchOnlyWalletManagerTest : LogsTestBase
    {        
        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWalletIsPresent_When_GetWatchOnlyWalletIsCalled_ThenthewalletIsreturned()
        {
            string dir = AssureEmptyDir("TestData/WatchOnlyWalletManagerTest/Given_AWalletIsPresent_When_GetWatchOnlyWalletIsCalled_ThenthewalletIsreturned");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);
            
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, Network.TestNet, dataFolder);
            walletManager.Initialize();

            // Retrieve the wallet.
            var returnedWallet = walletManager.GetWatchOnlyWallet();

            Assert.NotNull(returnedWallet);
            Assert.Equal(wallet.CreationTime.ToString("u"), returnedWallet.CreationTime.ToString("u"));
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AnAddressIsPassed_When_WatchAddressIsCalled_ThenAnAddressIsAddedToTheWatchList()
        {
            string dir = AssureEmptyDir("TestData/WatchOnlyWalletManagerTest/Given_AnAddressIsPassed_When_WatchAddressIsCalled_ThenAnAddressIsAddedToTheWatchList");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, Network.TestNet, dataFolder);
            walletManager.Initialize();

            // create the wallet
            Script newScript = new Key().ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(Network.TestNet).ToString();
            walletManager.WatchAddress(newAddress);

            var returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);
            Assert.True(returnedWallet.WatchedAddresses.ContainsKey(newScript.ToString()));
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_When_ATransactionIsReceived_ThenTransactionDataIsAddedToTheAddress()
        {
            // Arrange.
            string dir = AssureEmptyDir("TestData/WatchOnlyWalletManagerTest/Given_AWatchedAddress_When_ATransactionIsReceived_ThenTransactionDataIsAddedToTheAddress");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            // Create the wallet to watch.
            var wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Create the address to watch.
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", Network.TestNet).ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(Network.TestNet).ToString();

            // Create a transaction to be received.
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
            Transaction transaction = new Transaction(transactionHex);
            
            // Act.
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, Network.TestNet, dataFolder);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.ProcessTransaction(transaction);

            // Assert.
            var returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);

            var addressInWallet = returnedWallet.WatchedAddresses[newScript.ToString()];
            Assert.NotNull(addressInWallet);
            Assert.False(addressInWallet.Transactions.IsEmpty);
            Assert.Single(addressInWallet.Transactions);

            var transactionExpected = addressInWallet.Transactions.Single().Value;
            Assert.Equal(transactionHex, transactionExpected.Hex);
            Assert.Null(transactionExpected.BlockHash);
            Assert.Null(transactionExpected.MerkleProof);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_When_ATransactionIsReceivedInABlock_ThenTransactionDataIsAddedToTheAddress()
        {
            // Arrange.
            string dir = AssureEmptyDir("TestData/WatchOnlyWalletManagerTest/Given_AWatchedAddress_When_ATransactionIsReceivedInABlock_ThenTransactionDataIsAddedToTheAddress");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });

            // Create the wallet to watch.
            var wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Create the address to watch.
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", Network.TestNet).ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(Network.TestNet).ToString();

            // Create a transaction to be received.
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
            Transaction transaction = new Transaction(transactionHex);
            var block = new Block();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();

            // Act.
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, Network.TestNet, dataFolder);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.ProcessBlock(block);

            // Assert.
            var returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);

            var addressInWallet = returnedWallet.WatchedAddresses[newScript.ToString()];
            Assert.NotNull(addressInWallet);
            Assert.False(addressInWallet.Transactions.IsEmpty);
            Assert.Single(addressInWallet.Transactions);

            var transactionExpected = addressInWallet.Transactions.Single().Value;
            Assert.Equal(transactionHex, transactionExpected.Hex);
            Assert.NotNull(transactionExpected.BlockHash);
            Assert.NotNull(transactionExpected.MerkleProof);
        }

        /// <summary>
        /// Helper method that constructs a <see cref="WatchOnlyWallet"/> object and saved it to the file system.
        /// </summary>
        /// <param name="dataFolder">Folder location where the wallet will be saved,</param>
        /// <returns>The wallet that was created.</returns>
        private Features.WatchOnlyWallet.WatchOnlyWallet CreateAndPersistAWatchOnlyWallet(DataFolder dataFolder)
        {
            DateTime now = DateTime.Now;
            Script script = new Key().ScriptPubKey;
            uint256 transactionHash = uint256.One;
            string transactionHex = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff23034ba31100febc218159fe607004000963676d696e6572343208010000000000000000ffffffff02beaa8009000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed49141c29016cc30cf37fe64650ea78b93a894904380a97041570566fbe2d0d0c00000000";

            Features.WatchOnlyWallet.WatchOnlyWallet wallet = new Features.WatchOnlyWallet.WatchOnlyWallet
            {
                CoinType = CoinType.Bitcoin,
                Network = Network.TestNet,
                CreationTime = now,
                WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>()
            };

            wallet.WatchedAddresses.AddOrReplace(script.ToString(), new WatchedAddress
            {
                Script = script,
                Address = script.GetDestinationAddress(Network.TestNet).ToString(),
                Transactions = new ConcurrentDictionary<string, TransactionData>()
            });

            wallet.WatchedAddresses[script.ToString()].Transactions.AddOrReplace(transactionHash.ToString(), new TransactionData
            {
                BlockHash = uint256.Zero,
                Hex = transactionHex
            });

            Directory.CreateDirectory(dataFolder.WalletPath);
            File.WriteAllText(Path.Combine(dataFolder.WalletPath, "watch_only_wallet.json"), JsonConvert.SerializeObject(wallet, Formatting.Indented));
            return wallet;
        }
    }
}
