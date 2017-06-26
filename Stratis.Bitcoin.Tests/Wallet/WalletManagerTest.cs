using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Stratis.Bitcoin.FullNode;

namespace Stratis.Bitcoin.Tests.Wallet
{
    public class WalletManagerTest : TestBase
    {

        [Fact]
        public void UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/UpdateLastBlockSyncedHeightWhileWalletCreatedDoesNotThrowInvalidOperationException");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);
            Logs.Configure(loggerFactory.Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                                                  dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                                                  {
                                                      Cancellation = new System.Threading.CancellationTokenSource()
                                                  });

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

        [Fact]
        public void LoadKeysLookupInParallelDoesNotThrowInvalidOperationException()
        {
            string dir = AssureEmptyDir("TestData/WalletManagerTest/LoadKeysLookupInParallelDoesNotThrowInvalidOperationException");
            var dataFolder = new DataFolder(new NodeSettings { DataDir = dir });
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            Logs.Configure(loggerFactory.Object);

            var walletManager = new WalletManager(loggerFactory.Object, It.IsAny<ConnectionManager>(), Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new CancellationProvider()
                {
                    Cancellation = new System.Threading.CancellationTokenSource()
                });

            // generate 3 wallet with 2 accounts containing 1000 external and 100 internal addresses each.
            walletManager.Wallets.Add(CreateWallet("wallet1"));
            walletManager.Wallets.Add(CreateWallet("wallet2"));
            walletManager.Wallets.Add(CreateWallet("wallet3"));

            foreach (var wallet in walletManager.Wallets)
            {                
                wallet.AccountsRoot.Add(new AccountRoot
                {
                    CoinType = CoinType.Bitcoin,
                    Accounts = new List<HdAccount>
                    {
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(1000),
                            InternalAddresses = GenerateAddresses(1000)
                        },
                        new HdAccount
                        {
                            ExternalAddresses = GenerateAddresses(1000),
                            InternalAddresses = GenerateAddresses(1000)
                        } }
                });
            }
            
            Parallel.For(0, 5000, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, (int iteration) =>
            {
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
                walletManager.LoadKeysLookup();
            });
            
            Assert.Equal(12000, walletManager.keysLookup.Count);
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


    }
}
