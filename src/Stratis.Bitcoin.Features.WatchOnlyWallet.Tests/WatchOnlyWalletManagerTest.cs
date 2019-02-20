using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Tests
{
    public class WatchOnlyWalletManagerTest : LogsTestBase
    {
        private readonly Network networkTestNet;
        private readonly ISignals signals;

        public WatchOnlyWalletManagerTest()
        {
            this.networkTestNet = KnownNetworks.TestNet;
            this.signals = new Signals.Signals();
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWalletIsPresent_When_GetWatchOnlyWalletIsCalled_ThenthewalletIsreturned()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            WatchOnlyWallet wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();

            // Retrieve the wallet.
            WatchOnlyWallet returnedWallet = walletManager.GetWatchOnlyWallet();

            Assert.NotNull(returnedWallet);
            Assert.Equal(wallet.CreationTime.ToString("u"), returnedWallet.CreationTime.ToString("u"));
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AnAddressIsPassed_When_WatchAddressIsCalled_ThenAnAddressIsAddedToTheWatchList()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            WatchOnlyWallet wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();

            // create the wallet
            Script newScript = new Key().ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(this.networkTestNet).ToString();
            walletManager.WatchAddress(newAddress);

            WatchOnlyWallet returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);
            Assert.True(returnedWallet.WatchedAddresses.ContainsKey(newScript.ToString()));
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_When_ATransactionIsReceived_ThenTransactionDataIsAddedToTheAddress()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            // Create the wallet to watch.
            WatchOnlyWallet wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Create the address to watch.
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", this.networkTestNet).ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(this.networkTestNet).ToString();

            // Create a transaction to be received.
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
            Transaction transaction = this.networkTestNet.CreateTransaction(transactionHex);

            // Act.
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.ProcessTransaction(transaction);

            // Assert.
            WatchOnlyWallet returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);

            WatchedAddress addressInWallet = returnedWallet.WatchedAddresses[newScript.ToString()];
            Assert.NotNull(addressInWallet);
            Assert.False(addressInWallet.Transactions.IsEmpty);
            Assert.Single(addressInWallet.Transactions);

            TransactionData transactionExpected = addressInWallet.Transactions.Single().Value;
            Assert.Equal(transactionHex, transactionExpected.Hex);
            Assert.Null(transactionExpected.BlockHash);
            Assert.Null(transactionExpected.MerkleProof);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_When_ATransactionIsReceivedInABlock_ThenTransactionDataIsAddedToTheAddress()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            // Create the wallet to watch.
            WatchOnlyWallet wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Create the address to watch.
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", this.networkTestNet).ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(this.networkTestNet).ToString();

            // Create a transaction to be received.
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
            Transaction transaction = this.networkTestNet.CreateTransaction(transactionHex);
            Block block = this.networkTestNet.Consensus.ConsensusFactory.CreateBlock();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();

            // Act.
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.ProcessBlock(block);

            // Assert.
            WatchOnlyWallet returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);

            WatchedAddress addressInWallet = returnedWallet.WatchedAddresses[newScript.ToString()];
            Assert.NotNull(addressInWallet);
            Assert.False(addressInWallet.Transactions.IsEmpty);
            Assert.Single(addressInWallet.Transactions);

            TransactionData transactionExpected = addressInWallet.Transactions.Single().Value;
            Assert.Equal(transactionHex, transactionExpected.Hex);
            Assert.NotNull(transactionExpected.BlockHash);
            Assert.NotNull(transactionExpected.MerkleProof);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_When_ATransactionIsReceivedInABlock_ThenCanCalculateRelativeBalance()
        {
            // Arrange.
            DataFolder dataFolder = CreateDataFolder(this);

            // Create the wallet to watch.
            WatchOnlyWallet wallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Create the address to watch.
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", this.networkTestNet).ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(this.networkTestNet).ToString();

            // Create a transaction to be received.
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
            Transaction transaction = this.networkTestNet.CreateTransaction(transactionHex);
            Block block = this.networkTestNet.Consensus.ConsensusFactory.CreateBlock();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();

            // Act.
            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.ProcessBlock(block);

            // Assert.
            Money balance = walletManager.GetRelativeBalance("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            Assert.Equal(Money.Coins(1.60128533m), balance);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_ThenCanCalculateComplexRelativeBalance()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            // Create transactions to be received.
            var transactionsHex = new[]
            {
                "01000000018161735257ebaf9586bac9fd71955cbb833ea88a845d3a146d462c925c951c97010000001716001463a186b12a8e7c03d80eddc84c73f88e7ac41e3cffffffff02c03b4703000000001976a91454e37ad52373b08ad47dac9c909e2a86562740c088acfa4fc8fa0000000017a9148d26b9dc3a4d6105164567e37fe3a8e53fb60e958700000000",
                "0100000001cfc7db3f85932c636170ea95f3f77ea674283f981a6a1b36c9b2fd9a8248ea2c000000006b483045022100ba70c4538193e4a228666b8db58256c6bb9225f6fe39441a949f49f6964a947d022048508c50ed4a30df1f222d3dd010c10d5a0852d47843e96277c5b730b3194843012102a31bf228b4508abe3f0aa5f91c53732dfd49233c2fef3c5cfa68f2aa12cc71b4ffffffff02b0e13403000000001976a914546f3bb7a70a077d51bed1bf6acb55dead27024388ace0c810000000000017a914e3925ce02760a2fec19871938b0ae5c740fa2b908700000000",
                "0100000001a6391afc9adf3f385bd7261d6e120511203b60a9dacd3e99cc7010c7a22722ce0100000017160014096a4e906d7807137a10824ce68a0297a3d14e50ffffffff02e09da301000000001976a91454e37ad52373b08ad47dac9c909e2a86562740c088acb94b56e90000000017a91421649ed0b7412724abba0b27c3224beb3a7530e18700000000",
                "010000000180f6a5f49b79029333d1ab406424da4bd5f4378b19b1f8246b496f32cf215dc6000000006a4730440220080b0dfa1a93e8f4ebf9415727f60504df95c3a7dea7fe4e4053d0bb3f54540202202e9d02bbb642d7c55d637bd9a56265f42dc1626e933031d638507223846a5e53012102a31bf228b4508abe3f0aa5f91c53732dfd49233c2fef3c5cfa68f2aa12cc71b4ffffffff02225b9101000000001976a914e15f2eb7b19ae02102a1c24452fbd3f516dca8f888ace0c810000000000017a914585cffb7dc06ac82119fba0e36e1e95b534443958700000000",
                "01000000017f926a306647e0bf3ad12c86fca22dbce2f31161006d0723041963dbf31b17ae0100000017160014c72367e145246374dbb748fa274661cd7aece41effffffff02f0ced100000000001976a91454e37ad52373b08ad47dac9c909e2a86562740c088acf120a8d80000000017a9149bdfb11d7b870c364b4167d921339284b52ba1dc8700000000",
                "0100000001dd19f894733ab950ae8e772ef781fe299f80f4e813ee4cb6ac067fedea3a052c000000006b483045022100d4d6570c054bcbafa8178e9074f2be32b078e0761537bfe4fb7ad1eea949ab66022022e31d6f43d9418b6dc02147aefb380a4730386364bd406365e790826400570b012102a31bf228b4508abe3f0aa5f91c53732dfd49233c2fef3c5cfa68f2aa12cc71b4ffffffff029e90bf00000000001976a914128240d302a4aadcdd08d241b54fa4ef11acb21388ace0c810000000000017a914188d767b139ef64ce4efa091c2957c6137fbe0ce8700000000"
            };

            var block = this.networkTestNet.Consensus.ConsensusFactory.CreateBlock();
            foreach (string transactionHex in transactionsHex)
            {
                block.AddTransaction(this.networkTestNet.CreateTransaction(transactionHex));
            }

            block.UpdateMerkleRoot();

            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();
            walletManager.WatchAddress("moFoZk1figjfEe1Z49GUePXy2KqYr6DioL");
            walletManager.ProcessBlock(block);

            // Manual calculation of expected result
            // 2cea48829afdb2c9361b6a1a983f2874a67ef7f395ea7061632c93853fdbc7cf +0.55
            // c65d21cf326f496b24f8b1198b37f4d54bda246440abd1339302799bf4a5f680 +0.275
            // 2c053aeaed7f06acb64cee13e8f4809f29fe81f72e778eae50b93a7394f819dd +0.1375
            // 15c4f72e80a7607d87911969a418b18cc513b70588d27694179073ccdcf966f1 -0.1375
            // 3aeabae1ec7bd6362babf02b0bb51e4b5c183f30fbe593e170365afcdd5a6f7c -0.275
            // 240afed9840527f55f0087f4594a245219759307b034a983378d5dda91ea08a0 -0.55 = 0
            // Block explorer confirmation: https://testnet.smartbit.com.au/address/moFoZk1figjfEe1Z49GUePXy2KqYr6DioL

            Money balance = walletManager.GetRelativeBalance("moFoZk1figjfEe1Z49GUePXy2KqYr6DioL");
            Assert.Equal(Money.Coins(0m), balance);
        }

        [Fact]
        [Trait("Module", "WatchOnlyWalletManager")]
        public void Given_AWatchedAddress_And_A_WatchedTransaction_CanPopulateLookup()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            WatchOnlyWallet watchOnlyWallet = this.CreateAndPersistAWatchOnlyWallet(dataFolder);

            // Only need to watch a single address/transaction
            Script newScript = BitcoinAddress.Create("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE", this.networkTestNet).ScriptPubKey;
            string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";

            // Ensure transaction appears in block
            Transaction transaction = this.networkTestNet.CreateTransaction(transactionHex);
            Block block = this.networkTestNet.Consensus.ConsensusFactory.CreateBlock();
            block.AddTransaction(transaction);
            block.UpdateMerkleRoot();

            var walletManager = new WatchOnlyWalletManager(DateTimeProvider.Default, this.LoggerFactory.Object, this.networkTestNet, dataFolder, this.signals);
            walletManager.Initialize();
            walletManager.WatchAddress("mnSmvy2q4dFNKQF18EBsrZrS7WEy6CieEE");
            walletManager.StoreTransaction(new TransactionData()
            {
                Id = transaction.GetHash(),
                Hex = transactionHex
            });
            walletManager.ProcessBlock(block);

            WatchOnlyWallet wallet = walletManager.GetWatchOnlyWallet();

            // Artificially remove info from the watched address version of the transaction
            WatchedAddress addressInWallet = wallet.WatchedAddresses[newScript.ToString()];
            TransactionData watchedTransaction = addressInWallet.Transactions.Values.First();
            watchedTransaction.MerkleProof = null;
            watchedTransaction.BlockHash = null;

            // Now populate lookup
            ConcurrentDictionary<uint256, TransactionData> lookup = wallet.GetWatchedTransactions();

            // Expect that the cached version of the transaction has a
            // Merkle proof and block hash

            TransactionData lookupTransaction = lookup[transaction.GetHash()];

            Assert.NotNull(lookupTransaction.MerkleProof);
            Assert.NotNull(lookupTransaction.BlockHash);
        }

        /// <summary>
        /// Helper method that constructs a <see cref="WatchOnlyWallet"/> object and saved it to the file system.
        /// </summary>
        /// <param name="dataFolder">Folder location where the wallet will be saved,</param>
        /// <returns>The wallet that was created.</returns>
        private WatchOnlyWallet CreateAndPersistAWatchOnlyWallet(DataFolder dataFolder)
        {
            DateTime now = DateTime.Now;
            Script script = new Key().ScriptPubKey;
            uint256 transactionHash = uint256.One;
            string transactionHex = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff23034ba31100febc218159fe607004000963676d696e6572343208010000000000000000ffffffff02beaa8009000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed49141c29016cc30cf37fe64650ea78b93a894904380a97041570566fbe2d0d0c00000000";

            var wallet = new WatchOnlyWallet
            {
                CoinType = CoinType.Bitcoin,
                Network = this.networkTestNet,
                CreationTime = now,
                WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>()
            };

            wallet.WatchedAddresses.AddOrReplace(script.ToString(), new WatchedAddress
            {
                Script = script,
                Address = script.GetDestinationAddress(this.networkTestNet).ToString(),
                Transactions = new ConcurrentDictionary<string, TransactionData>()
            });

            wallet.WatchedAddresses[script.ToString()].Transactions.AddOrReplace(transactionHash.ToString(), new TransactionData
            {
                Id = this.networkTestNet.CreateTransaction(transactionHex).GetHash(),
                BlockHash = uint256.Zero,
                Hex = transactionHex
            });

            Directory.CreateDirectory(dataFolder.WalletPath);
            File.WriteAllText(Path.Combine(dataFolder.WalletPath, "watch_only_wallet.json"), JsonConvert.SerializeObject(wallet, Formatting.Indented));
            return wallet;
        }
    }
}