using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DBreeze.DataTypes;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Features.SQLiteWalletRepository.Tests
{
    public class TempDataFolder : DataFolder, IDisposable
    {
        private static string ClassNameFromFileName(string classOrFileName)
        {
            string className = classOrFileName.Substring(classOrFileName.LastIndexOf('\\') + 1);

            return className.Split(".")[0];
        }

        public TempDataFolder([CallerFilePath] string classOrFileName = "", [CallerMemberName] string callingMethod = "")
            : base(TestBase.AssureEmptyDir(TestBase.GetTestDirectoryPath(Path.Combine(ClassNameFromFileName(classOrFileName), callingMethod))))
        {
            try
            {
                Directory.Delete(TestBase.GetTestDirectoryPath(ClassNameFromFileName(classOrFileName)), true);
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
        }
    }

    public class MultiWalletRepositoryTests : RepositoryTests
    {
        public MultiWalletRepositoryTests() : base(true)
        {
        }
    }

    public class RepositoryTests
    {
        private readonly bool multiWallet;

        public RepositoryTests(bool multiWallet = false)
        {
            this.multiWallet = multiWallet;
        }

        [Fact]
        public void CanCreateWalletAndTransactionsAndAddressesAndCanRewind()
        {
            using (var dataFolder = new TempDataFolder(this.GetType().Name))
            {
                Network network = KnownNetworks.StratisRegTest;

                var repo = new SQLiteWalletRepository(dataFolder, network, DateTimeProvider.Default, new ScriptPubKeyProvider());

                repo.Initialize(this.multiWallet);

                var walletPassword = "test";
                var account = new WalletAccountReference("test2", "account 0");

                // Create an "test2" as an empty wallet.
                byte[] chainCode = Convert.FromBase64String("RUKVp47yWou1VNVBM1U2XYMUSRfJqisI0xATo17VLNU=");
                repo.CreateWallet(account.WalletName, "6PYQSX5vLVL2FtFWd5tDqk6KTCMEBubhdeFUL4xDRNhYueWR9iYNgiDDLV", chainCode);

                // Verify the wallet exisits.
                Assert.Equal("test2", repo.GetWalletNames().First());

                // Create "account 0" as P2PKH.
                repo.CreateAccount(account.WalletName, 0, account.AccountName, walletPassword, "P2PKH");

                // Create block 1.
                Block block1 = network.Consensus.ConsensusFactory.CreateBlock();
                BlockHeader blockHeader1 = block1.Header;

                // Create transaction 1.
                Transaction transaction1 = network.CreateTransaction();

                // Send 100 coins to the first unused address in the wallet.
                HdAddress address = repo.GetUnusedAddresses(account, 1).FirstOrDefault();
                transaction1.Outputs.Add(new TxOut(Money.COIN * 100, address.ScriptPubKey));

                // Add transaction 1 to block 1.
                block1.Transactions.Add(transaction1);

                // Process block 1.
                var chainedHeader1 = new ChainedHeader(blockHeader1, blockHeader1.GetHash(), null);
                repo.ProcessBlocks(new[] { (chainedHeader1, block1) }, account.WalletName);

                // List the unspent outputs.
                List<UnspentOutputReference> outputs1 = repo.GetSpendableTransactionsInAccount(account, chainedHeader1, 0).ToList();
                Assert.Single(outputs1);
                Assert.Equal(Money.COIN * 100, (long)outputs1[0].Transaction.Amount);

                // Create block 2.
                Block block2 = network.Consensus.ConsensusFactory.CreateBlock();
                BlockHeader blockHeader2 = block2.Header;
                blockHeader2.HashPrevBlock = blockHeader1.GetHash();

                // Create transaction 2.
                Transaction transaction2 = network.CreateTransaction();

                // Send the 90 coins to a fictituous external address.
                Script dest = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId());
                transaction2.Inputs.Add(new TxIn(new OutPoint(transaction1.GetHash(), 0)));
                transaction2.Outputs.Add(new TxOut(Money.COIN * 90, dest));

                // Send 9 coins change to my first unused change address.
                HdAddress address2 = repo.GetUnusedAddresses(account, 1, true).FirstOrDefault();
                transaction2.Outputs.Add(new TxOut(Money.COIN * 9, address2.ScriptPubKey));

                // Add transaction 2 to block 2.
                block2.Transactions.Add(transaction2);

                // Process block 2.
                var chainedHeader2 = new ChainedHeader(blockHeader2, blockHeader2.HashPrevBlock, chainedHeader1);
                repo.ProcessBlocks(new[] { (chainedHeader2, block2) }, account.WalletName);

                // List the unspent outputs.
                List<UnspentOutputReference> outputs2 = repo.GetSpendableTransactionsInAccount(account, chainedHeader2, 0).ToList();
                Assert.Single(outputs2);
                Assert.Equal(Money.COIN * 9, (long)outputs2[0].Transaction.Amount);

                // Check the wallet history.
                List<AccountHistory> accountHistories = repo.GetHistory(account.WalletName, account.AccountName).ToList();
                Assert.Single(accountHistories);
                List<FlatHistory> history = accountHistories[0].History.ToList();
                Assert.Equal(2, history.Count);

                // Verify 100 coins sent to first unused external address in the wallet.
                Assert.Equal("TTMM7qGGxD5c77pJ8puBg7sTLAm2zZNBwK", history[0].Address.Address);
                Assert.Equal("m/44'/105'/0'/0/0", history[0].Address.HdPath);
                Assert.Equal(0, history[0].Address.Index);
                Assert.Equal(Money.COIN * 100, (long)history[0].Transaction.Amount);

                // Looking at the spending tx we see 90 coins sent out and 9 sent to internal change address.
                List<PaymentDetails> payments = history[0].Transaction.SpendingDetails.Payments.ToList();
                Assert.Equal(2, payments.Count);
                Assert.Equal(Money.COIN * 90, (long)payments[0].Amount);
                Assert.Equal(dest, payments[0].DestinationScriptPubKey);
                Assert.Equal(Money.COIN * 9, (long)payments[1].Amount);
                Assert.Equal(address2.ScriptPubKey, payments[1].DestinationScriptPubKey);

                // Verify 9 coins sent to first unused change address in the wallet.
                Assert.Equal("TDGFEq1RsFKNQcATtHAivwtt5xLqfqbohe", history[1].Address.Address);
                Assert.Equal("m/44'/105'/0'/1/0", history[1].Address.HdPath);
                Assert.Equal(0, history[1].Address.Index);
                Assert.Equal(Money.COIN * 9, (long)history[1].Transaction.Amount);

                // REWIND: Remove block 1.
                repo.RewindWallet("test2", chainedHeader1);

                // List the unspent outputs.
                outputs1 = repo.GetSpendableTransactionsInAccount(account, chainedHeader1, 0).ToList();
                Assert.Single(outputs1);
                Assert.Equal(Money.COIN * 100, (long)outputs1[0].Transaction.Amount);

                // Check the wallet history.
                List<AccountHistory> accountHistories2 = repo.GetHistory(account.WalletName, account.AccountName).ToList();
                Assert.Single(accountHistories2);
                List<FlatHistory> history2 = accountHistories2[0].History.ToList();
                Assert.Single(history2);

                // Verify 100 coins sent to first unused external address in the wallet.
                Assert.Equal("TTMM7qGGxD5c77pJ8puBg7sTLAm2zZNBwK", history2[0].Address.Address);
                Assert.Equal("m/44'/105'/0'/0/0", history2[0].Address.HdPath);
                Assert.Equal(0, history2[0].Address.Index);
                Assert.Equal(Money.COIN * 100, (long)history2[0].Transaction.Amount);

                // Verify that the spending details have been removed.
                Assert.Null(history2[0].Transaction.SpendingDetails);
            }
        }

        [Fact(Skip = "Configure this test then run it manually.")]
        public void CanProcessBlocks()
        {
            using (var dataFolder = new TempDataFolder(this.GetType().Name))
            {
                Network network = KnownNetworks.StratisTest;

                var repo = new SQLiteWalletRepository(dataFolder, network, DateTimeProvider.Default, new ScriptPubKeyProvider());

                repo.Initialize(this.multiWallet);

                var walletPassword = "test";
                var account = new WalletAccountReference("test2", "account 0");

                // Create an "test2" as an empty wallet.
                byte[] chainCode = Convert.FromBase64String("RUKVp47yWou1VNVBM1U2XYMUSRfJqisI0xATo17VLNU=");
                repo.CreateWallet(account.WalletName, "6PYQSX5vLVL2FtFWd5tDqk6KTCMEBubhdeFUL4xDRNhYueWR9iYNgiDDLV", chainCode);

                // Verify the wallet exisits.
                Assert.Equal("test2", repo.GetWalletNames().First());

                // Create "account 0" as P2PKH.
                repo.CreateAccount(account.WalletName, 0, account.AccountName, walletPassword, "P2PKH");

                // Set up block store.
                string dataDir = @"E:\RunNodes\SideChains\Data\MainchainUser";
                var nodeSettings = new NodeSettings(network, args: new[] { $"-datadir={dataDir}" }, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION);
                DBreezeSerializer serializer = new DBreezeSerializer(network.Consensus.ConsensusFactory);
                IBlockRepository blockRepo = new BlockRepository(network, nodeSettings.DataFolder, nodeSettings.LoggerFactory, serializer);
                blockRepo.Initialize();

                var prevBlock = new Dictionary<uint256, uint256>();

                using (DBreeze.Transactions.Transaction transaction = blockRepo.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    byte[] hashBytes = uint256.Zero.ToBytes();

                    foreach (Row<byte[], byte[]> blockRow in transaction.SelectForward<byte[], byte[]>("Block"))
                    {
                        Array.Copy(blockRow.Value, sizeof(int), hashBytes, 0, hashBytes.Length);
                        uint256 hashPrev = serializer.Deserialize<uint256>(hashBytes);
                        var hashThis = new uint256(blockRow.Key);
                        prevBlock[hashThis] = hashPrev;
                    }
                }

                var nextBlock = prevBlock.ToDictionary(kv => kv.Value, kv => kv.Key);
                int firstHeight = 1;
                uint256 firstHash = nextBlock[network.GenesisHash];

                var chainTip = new ChainedHeader(new BlockHeader() { HashPrevBlock = network.GenesisHash }, firstHash, firstHeight);
                uint256 hash = firstHash;

                for (int height = firstHeight + 1; height <= blockRepo.TipHashAndHeight.Height; height++)
                {
                    hash = nextBlock[hash];
                    chainTip = new ChainedHeader(new BlockHeader() { HashPrevBlock = chainTip.HashBlock }, hash, chainTip);
                }

                var chainIndexer = new ChainIndexer(network, chainTip);

                for (int height = firstHeight ; height <= blockRepo.TipHashAndHeight.Height; )
                {
                    var buffer = new List<(ChainedHeader, Block)>();

                    for (int i = 0; i < 1000 && height <= blockRepo.TipHashAndHeight.Height; height++, i++)
                    {
                        ChainedHeader header = chainIndexer.GetHeader(height);
                        buffer.Add((header, blockRepo.GetBlock(header.HashBlock)));
                    }

                    repo.ProcessBlocks(buffer, "test2");
                }
            }
        }
    }
}
