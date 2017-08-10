using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;
using TransactionData = Stratis.Bitcoin.Features.WatchOnlyWallet.TransactionData;

namespace Stratis.Bitcoin.Tests.WatchOnlyWallet
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
            
            var walletManager = new WatchOnlyWalletManager(this.LoggerFactory.Object, Network.Main, dataFolder);
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

            var walletManager = new WatchOnlyWalletManager(this.LoggerFactory.Object, Network.Main, dataFolder);
            walletManager.Initialize();

            // create the wallet
            Script newScript = new Key().ScriptPubKey;
            string newAddress = newScript.GetDestinationAddress(Network.Main).ToString();
            walletManager.WatchAddress(newAddress);

            var returnedWallet = walletManager.GetWatchOnlyWallet();
            Assert.NotNull(returnedWallet);
            Assert.True(returnedWallet.WatchedAddresses.ContainsKey(newScript.ToString()));
        }

        /// <summary>
        /// Helper method that construcyts a <see cref="WatchOnlyWallet"/> object and saved it to the file system.
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
                Network = Network.Main,
                CreationTime = now,
                WatchedAddresses = new ConcurrentDictionary<string, WatchedAddress>()
            };

            wallet.WatchedAddresses.AddOrReplace(script.ToString(), new WatchedAddress
            {
                Script = script,
                Address = script.GetDestinationAddress(Network.Main).ToString(),
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
