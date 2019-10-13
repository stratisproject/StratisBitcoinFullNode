using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.External;
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

    public class MultiWalletRepositoryTests : WalletRepositoryTests
    {
        public MultiWalletRepositoryTests() : base(false)
        {
        }
    }

    public class ColdStakingDestinationReader : ScriptDestinationReader, IScriptDestinationReader
    {
        public ColdStakingDestinationReader(IScriptAddressReader scriptAddressReader) : base(scriptAddressReader)
        {
        }

        public override IEnumerable<TxDestination> GetDestinationFromScriptPubKey(Network network, Script redeemScript)
        {
            if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript, out KeyId hotPubKeyHash, out KeyId coldPubKeyHash))
            {
                yield return hotPubKeyHash;
                yield return coldPubKeyHash;
            }
            else
            {
                base.GetDestinationFromScriptPubKey(network, redeemScript);
            }
        }
    }

    public class BlockBase
    {
        public NodeSettings NodeSettings { get; private set; }
        public BlockRepository BlockRepo { get; private set; }
        public ChainIndexer ChainIndexer { get; private set; }

        internal Metrics Metrics { get; set; }

        public long TicksReading;

        public BlockBase(Network network, string dataDir, int blockLimit = int.MaxValue)
        {
            // Set up block store.
            this.NodeSettings = new NodeSettings(network, args: new[] { $"-datadir={dataDir}" }, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION);
            var serializer = new DBreezeSerializer(network.Consensus.ConsensusFactory);

            // Build the chain from the block store.
            this.BlockRepo = new BlockRepository(network, this.NodeSettings.DataFolder, this.NodeSettings.LoggerFactory, serializer);
            this.BlockRepo.Initialize();

            var prevBlock = new Dictionary<uint256, uint256>();

            using (DBreeze.Transactions.Transaction transaction = this.BlockRepo.DBreeze.GetTransaction())
            {
                transaction.ValuesLazyLoadingIsOn = true;

                byte[] hashBytes = uint256.Zero.ToBytes();

                foreach (Row<byte[], byte[]> blockRow in transaction.SelectForward<byte[], byte[]>("Block"))
                {
                    uint256 hashPrev = serializer.Deserialize<uint256>(blockRow.GetValuePart(sizeof(int), (uint)hashBytes.Length));
                    var hashThis = new uint256(blockRow.Key);
                    prevBlock[hashThis] = hashPrev;
                    if (prevBlock.Count >= blockLimit)
                        break;
                }
            }

            var nextBlock = prevBlock.ToDictionary(kv => kv.Value, kv => kv.Key);
            int firstHeight = 1;
            uint256 firstHash = nextBlock[network.GenesisHash];

            var genesis = new ChainedHeader(new BlockHeader(), network.GenesisHash, 0);
            var chainTip = new ChainedHeader(new BlockHeader() { HashPrevBlock = genesis.HashBlock }, firstHash, genesis);
            chainTip.Header.HashPrevBlock = network.GenesisHash;
            uint256 hash = firstHash;

            for (int height = firstHeight + 1; height <= this.BlockRepo.TipHashAndHeight.Height; height++)
            {
                hash = nextBlock[hash];
                chainTip = new ChainedHeader(new BlockHeader() { HashPrevBlock = chainTip.HashBlock }, hash, chainTip);
            }

            // Build the chain indexer from the chain.
            this.ChainIndexer = new ChainIndexer(network, chainTip);
            this.TicksReading = 0;
        }

        public IEnumerable<(ChainedHeader, Block)> TheSource()
        {
            for (int height = 0; height <= this.BlockRepo.TipHashAndHeight.Height;)
            {
                var hashes = new List<uint256>();
                for (int i = 0; i < 100 && (height + i) <= this.BlockRepo.TipHashAndHeight.Height; i++)
                {
                    ChainedHeader header = this.ChainIndexer.GetHeader(height + i);
                    hashes.Add(header.HashBlock);
                }

                long flagFall = DateTime.Now.Ticks;

                List<Block> blocks = this.BlockRepo.GetBlocks(hashes);

                if (this.Metrics != null)
                {
                    this.Metrics.ReadTime += (DateTime.Now.Ticks - flagFall);
                    this.Metrics.ReadCount += blocks.Count;
                }

                var buffer = new List<(ChainedHeader, Block)>();
                for (int i = 0; i < 100 && height <= this.BlockRepo.TipHashAndHeight.Height; height++, i++)
                {
                    ChainedHeader header = this.ChainIndexer.GetHeader(height);
                    yield return ((header, blocks[i]));
                }
            }
        }
    }

    public class WalletRepositoryTests
    {
        private Network network;
        private readonly bool dbPerWallet;
        private readonly string dataDir;
        private string[] walletNames;
        private static object lockTest = new object();

        public WalletRepositoryTests(bool dbPerWallet = true)
        {
            this.dbPerWallet = dbPerWallet;
            this.network = KnownNetworks.StratisTest;

            // Configure this to point to your "StratisTest" root folder and wallet.
            this.walletNames = new[] { "test2", "test" };
            this.dataDir = @"E:\RunNodes\SideChains\Data\MainchainUser";
        }

        static object lockObj = new object();

        [Fact]
        public void CanCreateWalletAndTransactionsAndAddressesAndCanRewind()
        {
            lock (lockTest)
            {
                using (var dataFolder = new TempDataFolder(this.GetType().Name))
                {
                    var nodeSettings = new NodeSettings(this.network, args: new[] { $"-datadir={dataFolder.RootPath}" }, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION);

                    var repo = new SQLiteWalletRepository(nodeSettings.LoggerFactory, dataFolder, this.network, DateTimeProvider.Default, new ColdStakingDestinationReader(new ScriptAddressReader()));
                    repo.WriteMetricsToFile = true;
                    repo.Initialize(this.dbPerWallet);

                    string password = "test";
                    var account = new WalletAccountReference("test2", "account 0");
                    var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                    ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, password);

                    // Create a wallet file.
                    string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();

                    // Create "test2" as an empty wallet.
                    byte[] chainCode = extendedKey.ChainCode;
                    ITransactionContext dbTran = repo.BeginTransaction(account.WalletName);
                    repo.CreateWallet(account.WalletName, encryptedSeed, chainCode);

                    // Get the extended pub key used to generate addresses for this account.
                    Key privateKey = Key.Parse(encryptedSeed, password, this.network);
                    var seedExtKey = new ExtKey(privateKey, chainCode);
                    ExtKey addressExtKey = seedExtKey.Derive(new KeyPath($"m/44'/{this.network.Consensus.CoinType}'/0'"));
                    ExtPubKey extPubKey = addressExtKey.Neuter();

                    repo.CreateAccount(account.WalletName, 0, account.AccountName, extPubKey);
                    dbTran.Commit();

                    // Verify the wallet exisits.
                    Assert.Equal(account.WalletName, repo.GetWalletNames().First());

                    // Create block 1.
                    Block block0 = this.network.Consensus.ConsensusFactory.CreateBlock();
                    BlockHeader blockHeader0 = block0.Header;
                    var chainedHeader0 = new ChainedHeader(blockHeader0, this.network.GenesisHash, null);

                    repo.ProcessBlock(block0, chainedHeader0, account.WalletName);

                    Block block1 = this.network.Consensus.ConsensusFactory.CreateBlock();
                    BlockHeader blockHeader1 = block1.Header;
                    blockHeader1.HashPrevBlock = this.network.GenesisHash;

                    // Create transaction 1.
                    Transaction transaction1 = this.network.CreateTransaction();

                    // Send 100 coins to the first unused address in the wallet.
                    HdAddress address = repo.GetUnusedAddresses(account, 1).FirstOrDefault();
                    transaction1.Outputs.Add(new TxOut(Money.COIN * 100, address.ScriptPubKey));

                    // Add transaction 1 to block 1.
                    block1.Transactions.Add(transaction1);

                    // Process block 1.
                    var chainedHeader1 = new ChainedHeader(blockHeader1, blockHeader1.GetHash(), chainedHeader0);
                    repo.ProcessBlock(block1, chainedHeader1, account.WalletName);

                    (Money totalAmount1, Money confirmedAmount1, Money spendableAmount1) = repo.GetAccountBalance(account, chainedHeader1.Height, 2);
                    Assert.Equal(new Money(100m, MoneyUnit.BTC), totalAmount1);
                    Assert.Equal(new Money(100m, MoneyUnit.BTC), confirmedAmount1);
                    Assert.Equal(new Money(0m, MoneyUnit.BTC), spendableAmount1);

                    // List the unspent outputs.
                    List<UnspentOutputReference> outputs1 = repo.GetSpendableTransactionsInAccount(account, chainedHeader1.Height, 0).ToList();
                    Assert.Single(outputs1);
                    Assert.Equal(Money.COIN * 100, (long)outputs1[0].Transaction.Amount);

                    // Create block 2.
                    Block block2 = this.network.Consensus.ConsensusFactory.CreateBlock();
                    BlockHeader blockHeader2 = block2.Header;
                    blockHeader2.HashPrevBlock = blockHeader1.GetHash();

                    // Create transaction 2.
                    Transaction transaction2 = this.network.CreateTransaction();

                    // Send the 90 coins to a fictituous external address.
                    Script dest = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId());
                    transaction2.Inputs.Add(new TxIn(new OutPoint(transaction1.GetHash(), 0)));
                    transaction2.Outputs.Add(new TxOut(Money.COIN * 90, dest));

                    // Send 9 coins change to my first unused change address.
                    HdAddress changeAddress = repo.GetUnusedAddresses(account, 1, true).FirstOrDefault();
                    transaction2.Outputs.Add(new TxOut(Money.COIN * 9, changeAddress.ScriptPubKey));

                    // Add transaction 2 to block 2.
                    block2.Transactions.Add(transaction2);

                    // Process block 2.
                    var chainedHeader2 = new ChainedHeader(blockHeader2, blockHeader2.HashPrevBlock, chainedHeader1);
                    repo.ProcessBlock(block2, chainedHeader2, account.WalletName);

                    (Money totalAmount2, Money confirmedAmount2, Money spendableAmount2) = repo.GetAccountBalance(account, chainedHeader1.Height, 2);
                    Assert.Equal(new Money(9m, MoneyUnit.BTC), totalAmount2);
                    Assert.Equal(new Money(9m, MoneyUnit.BTC), confirmedAmount2);
                    Assert.Equal(new Money(0m, MoneyUnit.BTC), spendableAmount2);

                    // List the unspent outputs.
                    List<UnspentOutputReference> outputs2 = repo.GetSpendableTransactionsInAccount(account, chainedHeader2.Height, 0).ToList();
                    Assert.Single(outputs2);
                    Assert.Equal(Money.COIN * 9, (long)outputs2[0].Transaction.Amount);

                    // Check the wallet history.
                    List<AccountHistory> accountHistories = repo.GetHistory(account.WalletName, account.AccountName).ToList();
                    Assert.Single(accountHistories);
                    List<FlatHistory> history = accountHistories[0].History.ToList();
                    Assert.Equal(2, history.Count);

                    // Verify 100 coins sent to first unused external address in the wallet.
                    Assert.Equal(address.Address, history[0].Address.Address);
                    Assert.Equal(address.HdPath, history[0].Address.HdPath);
                    Assert.Equal(0, history[0].Address.Index);
                    Assert.Equal(Money.COIN * 100, (long)history[0].Transaction.Amount);

                    // Looking at the spending tx we see 90 coins sent out and 9 sent to internal change address.
                    List<PaymentDetails> payments = history[0].Transaction.SpendingDetails.Payments.ToList();
                    Assert.Equal(2, payments.Count);
                    Assert.Equal(Money.COIN * 90, (long)payments[0].Amount);
                    Assert.Equal(dest, payments[0].DestinationScriptPubKey);
                    Assert.Equal(Money.COIN * 9, (long)payments[1].Amount);
                    Assert.Equal(changeAddress.ScriptPubKey, payments[1].DestinationScriptPubKey);

                    // Verify 9 coins sent to first unused change address in the wallet.
                    Assert.Equal(changeAddress.Address, history[1].Address.Address);
                    Assert.Equal(changeAddress.HdPath, history[1].Address.HdPath);
                    Assert.Equal(0, history[1].Address.Index);
                    Assert.Equal(Money.COIN * 9, (long)history[1].Transaction.Amount);

                    // FINDFORK
                    // See if FindFork can be run from multiple threads
                    var forks = new ChainedHeader[100];
                    Parallel.ForEach(forks.Select((f,n) => n), n =>
                    {
                        forks[n] = repo.FindFork("test2", chainedHeader2);
                    });

                    Assert.DoesNotContain(forks, f => f.Height != chainedHeader2.Height);

                    // REWIND: Remove block 1.
                    repo.RewindWallet(account.WalletName, chainedHeader1);

                    // FINDFORK
                    // See if FindFork can be run from multiple threads
                    forks = new ChainedHeader[100];
                    Parallel.ForEach(forks.Select((f, n) => n), n =>
                    {
                        forks[n] = repo.FindFork("test2", chainedHeader2);
                    });

                    Assert.DoesNotContain(forks, f => f.Height != chainedHeader1.Height);

                    // List the unspent outputs.
                    outputs1 = repo.GetSpendableTransactionsInAccount(account, chainedHeader1.Height, 0).ToList();
                    Assert.Single(outputs1);
                    Assert.Equal(Money.COIN * 100, (long)outputs1[0].Transaction.Amount);

                    // Check the wallet history.
                    List<AccountHistory> accountHistories2 = repo.GetHistory(account.WalletName, account.AccountName).ToList();
                    Assert.Single(accountHistories2);
                    List<FlatHistory> history2 = accountHistories2[0].History.ToList();
                    Assert.Single(history2);

                    // Verify 100 coins sent to first unused external address in the wallet.
                    Assert.Equal(address.Address, history2[0].Address.Address);
                    Assert.Equal(address.HdPath, history2[0].Address.HdPath);
                    Assert.Equal(0, history2[0].Address.Index);
                    Assert.Equal(Money.COIN * 100, (long)history2[0].Transaction.Amount);

                    // Verify that the spending details have been removed.
                    Assert.Null(history2[0].Transaction.SpendingDetails);

                    // Delete the wallet.
                    Assert.True(repo.DeleteWallet(account.WalletName));
                    Assert.Empty(repo.GetWalletNames());
                }
            }
        }

        private void LoadWallet(BlockBase blockBase, SQLiteWalletRepository repo, string walletName)
        {
            // Bypasses IsExtPubKey wallet check.
            Wallet wallet = new FileStorage<Wallet>(blockBase.NodeSettings.DataFolder.WalletPath).LoadByFileName($"{walletName}.wallet.json");

            // Create a new empty wallet in the repository.
            byte[] chainCode = wallet.ChainCode;
            repo.CreateWallet(walletName, wallet.EncryptedSeed, chainCode);

            // Verify the wallet exisits.
            Assert.Contains(repo.GetWalletNames(), w => w == walletName);

            // Clone the JSON wallet accounts.
            foreach (HdAccount hdAccount in wallet.GetAccounts())
            {
                var extPubKey = ExtPubKey.Parse(hdAccount.ExtendedPubKey);
                repo.CreateAccount(walletName, hdAccount.Index, hdAccount.Name, extPubKey);
            }
        }

        private void CanProcessBlocks(bool mainChain, string[] walletNames)
        {
            using (var dataFolder = new TempDataFolder(this.GetType().Name))
            {
                var network = mainChain ? KnownNetworks.StratisMain : KnownNetworks.StratisTest;
                var blockBase = new BlockBase(network, this.dataDir);

                // Initialize the repo.
                network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);
                var repo = new SQLiteWalletRepository(blockBase.NodeSettings.LoggerFactory, dataFolder, network, DateTimeProvider.Default, new ColdStakingDestinationReader(new ScriptAddressReader()));
                repo.WriteMetricsToFile = true;
                blockBase.Metrics = repo.Metrics;
                repo.Initialize(this.dbPerWallet);

                // Load the JSON wallet(s).
                foreach (string walletName in walletNames)
                    this.LoadWallet(blockBase, repo, walletName);

                long ticksTotal = DateTime.Now.Ticks;

                repo.ProcessBlocks(blockBase.TheSource());

                // Calculate statistics. Set a breakpoint to inspect these values.
                ticksTotal = DateTime.Now.Ticks - ticksTotal;

                // Now verify the DB against the JSON wallet(s).
                foreach (string walletName in walletNames)
                {
                    Wallet wallet = new FileStorage<Wallet>(blockBase.NodeSettings.DataFolder.WalletPath).LoadByFileName($"{walletName}.wallet.json");

                    foreach (HdAccount hdAccount in wallet.GetAccounts())
                    {
                        // Get the total balances.
                        (Money amountConfirmed, Money amountUnconfirmed) = hdAccount.GetBalances();

                        int walletHeight = (int)wallet.AccountsRoot.First().LastBlockSyncedHeight;

                        List<UnspentOutputReference> spendable = repo.GetSpendableTransactionsInAccount(
                            new WalletAccountReference(walletName, hdAccount.Name),
                            walletHeight).ToList();

                        Money amountRepo = spendable.Sum(s => s.Transaction.Amount);

                        Assert.Equal(amountConfirmed, amountRepo);
                    }
                }
            }
        }

        [Fact(Skip = "Configure this test then run it manually. Comment this Skip.")]
        public void CanProcessTestnetBlocks()
        {
            string[] walletNames = this.walletNames.ToArray();

            CanProcessBlocks(false, walletNames);
        }

        [Fact(Skip = "Configure this test then run it manually. Comment this Skip.")]
        public void CanProcessBinanceAddresses()
        {
            // 180 Binance addresses.
            var binance = new List<(string, double)>() {
                ("ScaS8mERgwyNW5Tg5cYHdXkHWRNRoBXZ28", 958856.37409301),
                ("SZPfzHd5KjAApJASNewveZq1GicNYVHbmE", 45002.0),
                ("SY2uLCCxHbvhE7CKETLjF5dDvFm4VrezGN", 29706.23251294),
                ("SZfLrdwCpm7DeYu3BxL3dGJkFr5Q7sNSA8", 13831.0),
                ("SYA79HrqXTzCFT86vp451v3fPgUHbRMcLB", 13823.67484463),
                ("Si3odKmR12fYxsRtzDf2oNdWLK6yJu6ufA", 10661.6381),
                ("SWXt3JTUm2DPrCGWLQB3qbjXromgThFyPj", 7817.0),
                ("SdFEoQSE7DsYqh9ib9VZWaUxWaUYqakRwF", 7336.0),
                ("SYsoRWi6DHoP2X3TThmRVsLWUf5FhGFHRG", 7030.0),
                ("SXTFHbiPciyX2C4LXiGyTuqGZuBN6yTZ4o", 6689.9042),
                ("SjwD6cTTCP5q9Y2zXK8wYGUDdpzfFpbVGx", 6602.0),
                ("SRoMoteUXvs82qCfGuGoHfQkkyNipmTrJd", 6178.29),
                ("ShnbABHf7p6KvMqQagzy5GDtsicHidNtwM", 5000.0),
                ("SdibbnoPgLTyHPwPepZBpBpZKzzBv8ddC8", 4649.01060534),
                ("SaMwRWjKezGUhKrGFBFKQpSJu1ZL7gZdkY", 4064.825),
                ("SZEEh6i4koVuoGNpKmniTWmrySm5ywCMCL", 3805.51887665),
                ("SgW5sNMZNqmFfAZyPYhWe1XgRqaEJh7cT3", 2967.0),
                ("SiAsCVZZxsrLDsWN97uspURNA64wQSDNgJ", 2604.25961019),
                ("SMZQb8bwgbAMRFgP3C5A1ZbC6N9gigWe3B", 2600.0),
                ("SP26YzaBQMSUxGzoBHMraFtvr4fswHjA2Q", 2365.29173784),
                ("SSbVSH91Fx73RVabFEHXySrBC2L8BGEeMP", 2011.8),
                ("SaEyiAAfRU7K1maFweyEQcdikchvpzwper", 1843.89),
                ("Sap5pWmZGrnA5kXLMyS3TDxNL78vHQTRSB", 1793.9),
                ("SMTdnzQCRLK43Hj9yFXFA5eNmvAYyVicav", 1608.36837206),
                ("SaSNHfX17qShzWWHATBivTB7d2JFzfjrj4", 1471.39),
                ("SeShi6yr21z3JgsZ4pw6v4DZG2JsWrcdMg", 1264.6),
                ("Sc7bbw1MM1smQqW2M4HiFHYABewdS3Fu5W", 1255.03137),
                ("SXz2Tw3ScDRtg7USUPhPG9mrjdTcn1gbvU", 1201.1),
                ("Sjj2YmMwieairVLGpmdCY31xk6NveFyv7Z", 1098.60377),
                ("ShAP6nGTjCgpdaBcxK88mZkuqHwbG39hTw", 1005.44481161),
                ("SZ1uJvSMUvoSbWyLqFKmS71StCWmmH2xD9", 1000.0),
                ("SPFvmksX5hXga3HevQaJzZhXAyPuDJ8H4p", 649.8186),
                ("SiCKxLkzRhMuAjmthxzrbomkR6A85kxdn7", 589.9997),
                ("SSVTcymmabfdn3ivRNdz1itzf2xDPXyt2K", 555.0),
                ("SV7HDpfYjZKjzgqSmpsEsxkwWm7QuqTM7f", 545.3074922),
                ("SaRZpSkpDAQmo56RZZ4zQNZkoGBUkC5dQR", 535.58427015),
                ("Se3jFhXfSLrwej26V9VaAe6GEk4EVX2WJ2", 430.36830119),
                ("ST8K8WyszM5DT2WKgKJ3xhC5inFXPfZCTg", 412.816529),
                ("SSr5EYYgFskjxcjNXY7sipiF1hVNaSCsTT", 400.0),
                ("SMyDr8jXzfcbvcact1frg5YVB8YXbCBjUS", 400.0),
                ("SUo3hFis9K6v3SiPTBDEZrsxCv3b6Q4T78", 392.87),
                ("SWqTi9fMyG1bMk6qVQcjBLMdHCuB5JZwKS", 388.53791),
                ("SiamNLGVf7ChexRqetqWqSEfafx7CzdcMx", 378.313774),
                ("SczVNQkSSVgNqAYMDM6QXQHbp5Z66AsEgR", 366.8),
                ("SiJhcw1mGXpZYHMFXREv2AgtmYVEehF8ug", 362.366),
                ("SPe9yNnc6Cif7BXBVQdUzV3L93JU3DrqVa", 333.72972),
                ("SjJ1YLnXtR7J1e1sAFgX9nbLX2EdRpG7wU", 320.27019223),
                ("Si95MS7UkAaNsTHnnxgbCEyBhZAYWdTFPe", 306.92241536),
                ("SZwjhzERF9grwrBjGxSoA4UkfbLFvCd3hV", 256.0),
                ("SWHHpcx4hFKN7pEtMqP1rBwfGJoac2xoUb", 200.0),
                ("Sj9qrecwNnBmqYkAnpv6cskFMHCvHmhR58", 159.98),
                ("ShJrt32WxSg9CzZFEDghWPbZFiQZjNb8SZ", 144.62084),
                ("SZzdaN8UNcU6JSy2ZpeUsyhQS4Mm5pmdwu", 140.92186689),
                ("Sa74kB7Zv8cTCRHWfpGZ37KgWXQMxJ2mYU", 100.0),
                ("SiP2695QdRPGocEysZwvnwMVuMm4Ci38v6", 100.0),
                ("SUWYtmUkSsxjoVFhDhs9zT5YgcSeLDiWDy", 100.0),
                ("ScqwL3Jaf6xuubuxLj48LfA2iKsD5wB7qu", 80.0),
                ("SXXNtPkgJ6swWFoF6yrd5dhZ4Ma26zvrh6", 71.07000002),
                ("SZpssbHtsJDbxvkLTSYZGknbgVK8n4sr6W", 54.097),
                ("SRMCiQWnr3XYiSdXFfoiQLTvcqShCVw44M", 51.0),
                ("SSYMVMCe48ynHQncgfVEGjGV7Xmng7BvfG", 49.99),
                ("Sg2Sg7D7yByEePUNXhT2WWUL3buKbYARef", 46.17973762),
                ("SYvvWRKAc6JbaaA6mn6LMi44DdKHqdmh3D", 36.0),
                ("SN7V4nsHD5fUXdQLrTmrBxfz8XQKywf6Rb", 33.955),
                ("SgP3LqAgC3xRGDUS1qJbRAZA6Uh7RY32WM", 30.5),
                ("SQadGFNXHgjs2mbTLkvXuv1fo8DqxQcP2j", 24.185182),
                ("SXFHwa4VALtGmnzVt8GpHsneJNgnojV2jV", 23.11270501),
                ("SRGwFXDMBhcogTVL6EnnwSg7npiSyvgrpA", 15.0),
                ("SU2dibovtEmtJYt7kxomD5avaGB3yPFXgD", 11.96286211),
                ("Sb94hnn4VnYezTo6s6cAexoe64oJLeEqHK", 10.0),
                ("SgBSWDGfb1J2rJGYPqsQjiipV3bXbxL8ij", 10.0),
                ("SVGkKZia82ZUv7zHaHcJC5aPs2mBhmUSGc", 10.0),
                ("SbqnBkKxzCvoJzwru8VQQ4PPEG1RXKGY3H", 10.0),
                ("SfvJjaoSy2sWpprs112GZEazrpjvHSqXGs", 10.0),
                ("SYyTZPhjyVhimtykyxRq8xsVKw1vj5cZSr", 9.00906319),
                ("SaTE9ecfEMAh2CVpsQjmej4uhizxxSAXS2", 7.6999),
                ("SRksszbWQKNEr4S8sAF8E2r9ZC4gkwne1K", 3.0),
                ("SgEyYzRpkeqessEs8mtk2wKrMBreB7u9Sr", 2.97507767),
                ("Sbq5Ls1xWqPwJo2D8vZkV9ZMVXSbyaT6oV", 1.16903751),
                ("SaoQ5VnNyzGLf3REqYmW8rAubspQShupFr", 1.0801332),
                ("SQMLvJwPm4TyfiABZN9miqzBVD2X5gG5g1", 1.0),
                ("SVJ2RymTmLduXbVjoqqxgfx9ubHmo9a6bK", 1.0),
                ("SctwtdgtDX2nsAfNvkWtPkMGFgXYxvSBrW", 0.5),
                ("SSL9RKkBhwAGPnB7XQEuA5ARJSfVJ87HWS", 0.28020936),
                ("SjJAehQE2SxhSuvkF5JSxL6QP6WNdbJHb4", 0.18),
                ("SWc7sidFJ2UUQWfBJ4WzfgvJkvNKXrHwrD", 0.15129548),
                ("SgjDSf7s4HxXi8dsfcTtebn7d5Lo8AUnBR", 0.13641482),
                ("SZvYNyUBpMBjoou5dntCvPrcSb5sGkLd9g", 0.12995138),
                ("Sfzj2Jnn4oSuwvzCr1QynZThNpm6qyotix", 0.11571148),
                ("SkHbQCTjq8tWWbWbb4MC3ZwNBNo8qU9rJP", 0.10992201),
                ("SXZoGabSkCHuW4GWJz4RgTtYXE2G2WVWEx", 0.10804804),
                ("Sc19SnotFy8wgbjoZN4vM4NCHdaWtwxRWg", 0.10758822),
                ("SZHb38FhaWnvSn5jrYXKyWFbRpZ6pBcUQ7", 0.10279537),
                ("Sb8495Wdurk2Fn6wARQdg1xE8J4aw3HByM", 0.08743127),
                ("Sa7pWzdzMBaSG1rKXfZYDj1joaS38yfYdc", 0.08457213),
                ("SYwkhMFGibZxNPphG8WSArowAu45qECMLb", 0.0842431),
                ("SgBnVD7Xbz9e51SVorEL1d29dAVoer4tLF", 0.06908402),
                ("SPQxFLQUPKEPdLfpP6eA2u6Vav6HLQ1CEq", 0.06847339),
                ("SYE2hJQodBBDoXxSGa4zfvfVhPwa3HsJPb", 0.06224305),
                ("SYa6CwgMJTWM1iZSArzBMAWQYZEP8PmtMb", 0.06210968),
                ("SZkXnM4TyPkgTesoC4YhBGHhzepp4HLkj7", 0.05920034),
                ("SX6G6Dvr1DuC62gaLmDvN41x4eQ9rntBrg", 0.05786474),
                ("SXFkz95e2TorLq42VLACovz3UKX1MzdjTb", 0.05670634),
                ("ScJjfyg9QCFBWk8Vj1Ed5JKEZM8F1vz2GZ", 0.0428956),
                ("SWEH3H1EbfqD29jZxV2cRAJVKcZEj1PbRn", 0.04253237),
                ("SbszcZX4WrcxpQ9C67Uwy9dX3PcgoDm3xr", 0.04158475),
                ("SVL6WXhojDbQCs1xFE27PdA8nmLBHM7bXa", 0.03736873),
                ("SZo5D6sw4MuRxK8meKEZv3PRuY5EwUEhL7", 0.0368388),
                ("SXKiSMP34R8NgS8mD6pn3BG9tUPHe4SzyG", 0.03649525),
                ("SWkVi9xigCDzMYRWdcsXuMRGYdCiLg9Xxk", 0.03615241),
                ("SZDgzBfwxc6HuxBVmmeqFGCXNx6KiGJVon", 0.03525494),
                ("SXjLTTBWV6pugjv8h6WeTBb1BCbM4tgPbH", 0.03358),
                ("Sf7c5X6JqHogqSh4SU4p8XK4jFVp1PryFk", 0.0299),
                ("Scxtf4AeHZU6mViJyhiZerr1i2ed4xXEmz", 0.02650688),
                ("SbwHEz9ic4yjuTTobATfkyUcfhsDAV8Ygt", 0.02482007),
                ("SYmURk78JUTm65dh7EtPLz6SndxZy1Jd3M", 0.02444891),
                ("SYVo7LA16X2uYtBX9GNTtFgWVM3idL9J6C", 0.02323409),
                ("SW9jPAr5dZHU5cZPWuNnNX4oAFoHLwQKzY", 0.02141639),
                ("SVPWT3yx5TFLfQHdWLtdcYBtWszuUjSvQg", 0.02031755),
                ("SZW3DCHZURXEUMGuHqbbo1p9P8mEFeYn1e", 0.01859882),
                ("SXbu7jggxt3unRTrQjgJX74kt6nXv7NwFd", 0.01847456),
                ("SX8TKCiYnKobd8ufDxDgcgTmumAJzKn6Qn", 0.01812015),
                ("SWRVVSqe2Y4FNjMnQgeUi8pkAugyDwFSPQ", 0.01804139),
                ("SXzLnbkrJzQSnygafwiAYDLt6DRizPLBLw", 0.01727822),
                ("SZARz8hYSV2BDjSYEFJHoKgJ92qJi9cf5d", 0.01604225),
                ("SXu9AXjr1aWxzjhn1vw2hmCxiy1qmGjWBp", 0.0151411),
                ("SWaBUryo4h1sqfB7AjQJJZ1rsYeEeGU42v", 0.01470254),
                ("SXQ67vq183khneBQcDr9cSMDvzihCLVaSx", 0.01450288),
                ("SZKfQcsmSzAUad66P4BPnPDUKDpK4eTT7B", 0.01420712),
                ("Sb3ZPWVioAMDeokFuaYHoRxsRS8TzCCvvT", 0.01145836),
                ("SXe7U1DqrAzp3Szo4KRf5AvkWR7dUwyasb", 0.01128214),
                ("SXmPnivJTT16kmhBtcjSL6ij7rKxjbDGna", 0.01101061),
                ("SfitueMdHBNhip5dbqEGPBNswf2rqmZZMC", 0.01022391),
                ("SWfPgiwvgHwZYLi9Kbes9m2xqnjKCvBpSs", 0.01),
                ("SWNjfTuqPxu9DuYtj9nNojyQpynYzep86R", 0.00864555),
                ("Sk2TEojTQvT3zKr8ywEhrd4dh55MhaKpNs", 0.00857307),
                ("SdNu5QFTzYBrbDdag1HNwSFjmEeMG1WwcH", 0.00838976),
                ("SfTzbDmyLTsHmafDCDJvwU8BApE9fev6B5", 0.00812732),
                ("SedFLaoBtSsskLRUrSHEci5cTiNKN3XgEH", 0.00765587),
                ("SXVKgL7Q4kbjAM9Jy4FX2DYrRoLsD4qjKY", 0.00698436),
                ("SXq5Pr1HArjFVjtQRJ9qhPPwPoLLVFtWMa", 0.00584235),
                ("SVL4dVamLB3tfAZjpi1P38FtGh5RKxb2es", 0.00505273),
                ("SYPoBswUAtE9gAPJ12ZJaCXWb16SNeHtYp", 0.00411987),
                ("SVEpaPYsqUx8NSRYo8PGEsPye9HA3iUS7e", 0.00384549),
                ("SW5K6b3zCZUksg5j5nxmpyFPb4Vv8x8qpX", 0.00383955),
                ("SUqYhjr37sbB9NvoT4vV9X2auX5DnwsFb7", 0.00381675),
                ("SXKudGmo8JNoTsJUCAXvryWQ3iKzAmtnLi", 0.00369427),
                ("SZNY3nrsxoetcuHHxbPFRcBSG1oSAznx1h", 0.00337854),
                ("Sc6J9K4dMWZWDjoAhhoNKPFuyne93SGAeY", 0.00314479),
                ("Shuj8bGfYqYuTKoAu8oVC3oWrDHQMyjZoa", 0.00311107),
                ("SVjHVSj9cPC1Jp2VmhviD6XuyqsjivEYPd", 0.00300433),
                ("SYjFdTZwQPN8DEJMQ3YkefhoXm6EJnmXwQ", 0.00239602),
                ("SVJqTaPFFDTGDBsG3d3Bwu28iDCkAq3Sgt", 0.00218232),
                ("SZQvPjfEbxzhmnQ4PR9za1PjDqXbfDtboP", 0.00185927),
                ("SWK8u9JwiqsmuUobEZrDo3cCdbszMZRVaw", 0.00185332),
                ("SW4DSMZhXfjtrg17xda8tEcNakkTVRTibE", 0.00169781),
                ("SVkV1soPKNTy7ZeNSNfeoafPXdqAALcfa4", 0.00153524),
                ("ScYDPxJAZxbWqj3qwZeP7gduCHzbtrng8C", 0.00147015),
                ("SY3Gt1re7eFgY6ULzTLVadf85S3fTYAype", 0.00145292),
                ("SVxFDVNDvGoJCF8yCeWvZswzHkVUPbdH2H", 0.00143327),
                ("SXVxnJaLyuu9CjmzVWjJyjB5H5dLHy2anH", 0.00142176),
                ("SVjcZH7euWa9Z43C7wVaCuXCRD9HTVtFpR", 0.00136181),
                ("SVMYQTSVqHqCthUCCj7nvSTfyb4rFpP25Y", 0.0013304),
                ("SXn8nUsQU5ZiETSZFnsscsvZ7FynSxZ2v1", 0.00123051),
                ("SVKa6Cc91DfgXJumyAWBQNzRdivaGEUVJV", 0.0012187),
                ("SXatTTDz3M4z87d8XgzupAEBN2cwNxojzB", 0.00113311),
                ("SVPdqi9YdkczNZvz65H7LsAWvBmx47pgXT", 0.00113256),
                ("SVJKwoHiuU6LrzRpKR6aYqZBpTWWzKyA1f", 0.00111486),
                ("SW6uMn8Mj1QZQAkqmEoNJzDYdeffg1b7GJ", 0.00110208),
                ("SWngXu9L21uBm24xWoBDysW3jxUTLbBBLD", 0.00108936),
                ("SWNUpCrcrCzFPCbhwbjEYgAh1RM9kTuP2m", 0.00100873),
                ("Sje7D1q8GM6wqS1udYzBiuQGEXM7ZpjDWo", 0.00095482),
                ("SVnewD3VHvk2fPKY1DXwwvSmmVrX6UnWcw", 0.00092325),
                ("SU33eLogL99Mvmyz1VmtfMoSFe4dvdwSMQ", 0.00089698),
                ("Sa42iXtzFME5KqNVKUiGz6BeUqrWykkQRq", 0.00083003),
                ("SWdg455Rzus6k9rJm1tcJU1LcVZgTTpW9T", 0.00059667),
                ("SVR6irN2buyMzfsDLpGwc2gQga58wRFWCJ", 0.00056065),
                ("SVLzMguNxKX9JgT1BkhhNAdTR5MptewLU8", 0.00047487),
                ("SWwob4MHXrAmLidf8koftgGqK1Xit2n3Wy", 0.00046032),
                ("SWByhiNuNWLfntkFwiTRZ1JjpeDWuyvnMv", 0.00043353)
                };

            using (var dataFolder = new TempDataFolder(this.GetType().Name))
            {
                var network = KnownNetworks.StratisMain;
                var blockBase = new BlockBase(network, this.dataDir);

                // Initialize the repo.
                network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);
                var repo = new SQLiteWalletRepository(blockBase.NodeSettings.LoggerFactory, dataFolder, network, DateTimeProvider.Default, new ColdStakingDestinationReader(new ScriptAddressReader()));
                repo.WriteMetricsToFile = true;
                blockBase.Metrics = repo.Metrics;
                repo.Initialize(this.dbPerWallet);

                // Create a watch-only wallet.
                repo.CreateWallet("wallet1", null, null);
                repo.CreateAccount("wallet1", 0, "account 0", (ExtPubKey)null);
                repo.AddWatchOnlyAddresses("wallet1", "account 0", 0, binance
                    .Select(b => b.Item1)
                    .Select(addr => new HdAddress() { ScriptPubKey = BitcoinAddress.Create(addr, network).ScriptPubKey })
                    .ToList());

                // Process the blocks and calculate statistics.
                long ticksTotal = DateTime.Now.Ticks;
                repo.ProcessBlocks(blockBase.TheSource());
                ticksTotal = DateTime.Now.Ticks - ticksTotal;
            }
        }
    }
}