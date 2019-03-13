using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    public class MempoolPersistenceTest : IDisposable
    {
        private readonly string dir;
        private readonly Network network;
        private readonly bool shouldDeleteFolder = false;

        public MempoolPersistenceTest()
        {
            this.dir = "TestData/MempoolPersistenceTest/";

            if (!Directory.Exists(this.dir))
            {
                this.shouldDeleteFolder = true;
                Directory.CreateDirectory(this.dir);
            }

            this.network = KnownNetworks.Main;
        }

        public void Dispose()
        {
            if (this.shouldDeleteFolder)
                Directory.Delete(this.dir, true);
        }

        [Fact]
        public void SaveLoadFileTest()
        {
            int numTx = 22;
            string fileName = "mempool.dat";
            NodeSettings settings = this.CreateSettings("SaveLoadFileTest");
            var persistence = new MempoolPersistence(settings, new LoggerFactory());
            IEnumerable<MempoolPersistenceEntry> toSave = this.CreateTestEntries(numTx);
            IEnumerable<MempoolPersistenceEntry> loaded;

            MemPoolSaveResult result = persistence.Save(settings.Network, toSave, fileName);
            loaded = persistence.Load(settings.Network, fileName);

            Assert.True(File.Exists(Path.Combine(settings.DataDir, fileName)));
            Assert.True(result.Succeeded);
            Assert.Equal((uint)numTx, result.TrxSaved);
            Assert.Equal(loaded, toSave.ToArray());
        }

        [Fact(Skip = "Unstable on ubuntu")]
        public void LoadBadFileTest()
        {
            int numTx = 22;
            string fileName = "mempool.dat";
            NodeSettings settings = this.CreateSettings("LoadBadFileTest");
            var persistence = new MempoolPersistence(settings, new LoggerFactory());
            IEnumerable<MempoolPersistenceEntry> toSave = this.CreateTestEntries(numTx);
            IEnumerable<MempoolPersistenceEntry> loaded;
            string fullFilePath = Path.Combine(settings.DataDir, fileName);

            MemPoolSaveResult result = persistence.Save(settings.Network, toSave, fileName);
            string fileData = File.ReadAllText(fullFilePath);
            string badFileData = new string(fileData.Take(fileData.Length / 2).ToArray());
            File.WriteAllText(fullFilePath, badFileData);
            loaded = persistence.Load(settings.Network, fileName);

            Assert.True(File.Exists(fullFilePath));
            Assert.True(result.Succeeded);
            Assert.Null(loaded);
        }

        [Fact]
        public void LoadNoFileTest()
        {
            string fileName = "mempool.dat";
            NodeSettings settings = this.CreateSettings("LoadNoFileTest");
            var persistence = new MempoolPersistence(settings, new LoggerFactory());
            string fullFilePath = Path.Combine(settings.DataDir, fileName);

            IEnumerable<MempoolPersistenceEntry> loaded = persistence.Load(settings.Network, fileName);

            Assert.False(File.Exists(fullFilePath));
            Assert.Null(loaded);
        }

        [Fact]
        public void LoadPoolTest_WithBadTransactions()
        {
            int numTx = 5;
            string fileName = "mempool.dat";
            NodeSettings settings = this.CreateSettings("LoadPoolTest_WithBadTransactions");
            IEnumerable<MempoolPersistenceEntry> toSave = this.CreateTestEntries(numTx);
            MempoolManager mempoolManager = CreateTestMempool(settings, out TxMempool unused);

            MemPoolSaveResult result = (new MempoolPersistence(settings, new LoggerFactory())).Save(settings.Network, toSave, fileName);
            mempoolManager.LoadPoolAsync(fileName).GetAwaiter().GetResult();
            long actualSize = mempoolManager.MempoolSize().GetAwaiter().GetResult();

            Assert.Equal(0, actualSize);
        }

        [Fact]
        public async Task LoadPoolTest_WithGoodTransactionsAsync()
        {
            string fileName = "mempool.dat";
            Transaction tx1_parent = this.network.CreateTransaction("0100000001c4fadb806f9679c27c30c11b694523f6ac9614f7a69076b8940082ce636040fb000000006b4830450221009ad4b969a40b95017d133b13f7d465031829731f3b0ae4bcdcb5e393f5e919f902207f33aad2c3af48d6d65aaf5dd15a85a1f588ee3d6f477b2236cda1d81d88c43b012102eb184a906e082db44a95347de64110952b5821c42068a2054947aec4bc60db2fffffffff02685e3e00000000001976a9149ed35c9c42543ec67f9e6d1033e2ac1ac76f86ba88acd33e4500000000001976a9143c88fada9101f660d77feec1dd8db4ee9ea01d6788ac00000000");
            Transaction tx1 = this.network.CreateTransaction("0100000001055c4c42511f9d05f2fa817c7f023df567f3d501bebec14ddce7c05a9d5fda52000000006b483045022100de552f011768887141b9a767ae184f61aa3743a32aad394ac1e1ec35345415420220070b3d0afd28414f188c966e334e9f7b65e7440538d93bc1d61f82067fcfd3fa012103b47b6ffce08f54be286620a29f45407fedb7b33acfec938551938ec96a1e1b0bffffffff019f053e000000000017a91493e31884769545a237f164aa07b3caef6b62f6b68700000000");
            NodeSettings settings = this.CreateSettings("LoadPoolTest_WithGoodTransactions");
            TxMempool txMemPool;
            MempoolManager mempoolManager = CreateTestMempool(settings, out txMemPool);
            Money fee = Money.Satoshis(0.00001m);

            txMemPool.AddUnchecked(tx1_parent.GetHash(), new TxMempoolEntry(tx1_parent, fee, 0, 0.0, 0, tx1_parent.TotalOut + fee, false, 0, null, new ConsensusOptions()));
            long expectedTx1FeeDelta = 123;

            // age of tx = 5 hours
            long txAge = 5 * 60 * 60;

            var toSave = new List<MempoolPersistenceEntry>
            {
                new MempoolPersistenceEntry{
                    Tx = tx1,
                    Time = mempoolManager.DateTimeProvider.GetTime() - txAge,
                    FeeDelta = expectedTx1FeeDelta
                },
            };
            MemPoolSaveResult result = (new MempoolPersistence(settings, new LoggerFactory())).Save(settings.Network, toSave, fileName);

            long expectedSize = 2;
            await mempoolManager.LoadPoolAsync(fileName);
            long actualSize = await mempoolManager.MempoolSize();
            TxMempoolEntry actualEntry = txMemPool.MapTx.TryGet(tx1.GetHash());
            long? actualTx1FeedDelta = actualEntry?.feeDelta;

            Assert.Equal(expectedSize, actualSize);
            Assert.Equal(expectedTx1FeeDelta, actualTx1FeedDelta);
        }

        [Fact]
        public async Task LoadPoolTest_WithExpiredTransaction_PurgesTxAsync()
        {
            string fileName = "mempool.dat";
            Transaction tx1_parent = this.network.CreateTransaction("0100000001c4fadb806f9679c27c30c11b694523f6ac9614f7a69076b8940082ce636040fb000000006b4830450221009ad4b969a40b95017d133b13f7d465031829731f3b0ae4bcdcb5e393f5e919f902207f33aad2c3af48d6d65aaf5dd15a85a1f588ee3d6f477b2236cda1d81d88c43b012102eb184a906e082db44a95347de64110952b5821c42068a2054947aec4bc60db2fffffffff02685e3e00000000001976a9149ed35c9c42543ec67f9e6d1033e2ac1ac76f86ba88acd33e4500000000001976a9143c88fada9101f660d77feec1dd8db4ee9ea01d6788ac00000000");
            Transaction tx1 = this.network.CreateTransaction("0100000001055c4c42511f9d05f2fa817c7f023df567f3d501bebec14ddce7c05a9d5fda52000000006b483045022100de552f011768887141b9a767ae184f61aa3743a32aad394ac1e1ec35345415420220070b3d0afd28414f188c966e334e9f7b65e7440538d93bc1d61f82067fcfd3fa012103b47b6ffce08f54be286620a29f45407fedb7b33acfec938551938ec96a1e1b0bffffffff019f053e000000000017a91493e31884769545a237f164aa07b3caef6b62f6b68700000000");
            NodeSettings settings = this.CreateSettings("LoadPoolTest_WithExpiredTxs");
            TxMempool txMemPool;
            MempoolManager mempoolManager = CreateTestMempool(settings, out txMemPool);
            Money fee = Money.Satoshis(0.00001m);

            txMemPool.AddUnchecked(tx1_parent.GetHash(), new TxMempoolEntry(tx1_parent, fee, 0, 0.0, 0, tx1_parent.TotalOut + fee, false, 0, null, new ConsensusOptions()));
            long expectedTx1FeeDelta = 123;

            // age of tx = 5 hours past expiry
            long txAge = (MempoolValidator.DefaultMempoolExpiry + 5) * 60 * 60;

            var toSave = new List<MempoolPersistenceEntry>
            {
                new MempoolPersistenceEntry{
                    Tx = tx1,
                    Time = mempoolManager.DateTimeProvider.GetTime() - txAge,
                    FeeDelta = expectedTx1FeeDelta
                },
            };
            MemPoolSaveResult result = (new MempoolPersistence(settings, new LoggerFactory())).Save(settings.Network, toSave, fileName);

            long expectedSize = 1;
            await mempoolManager.LoadPoolAsync(fileName);
            long actualSize = await mempoolManager.MempoolSize();
            TxMempoolEntry actualEntry = txMemPool.MapTx.TryGet(tx1.GetHash());
            Assert.Null(actualEntry);
            Assert.Equal(expectedSize, actualSize);
        }

        [Fact]
        public void SaveStreamTest()
        {
            int numTx = 22;
            int expectedLinesPerTransaction = 3;
            int expectedHeaderLines = 2;
            int expectedLines = numTx * expectedLinesPerTransaction + expectedHeaderLines;
            var settings = new NodeSettings(this.network, args: new string[] { $"-datadir={ Path.Combine(this.dir, "SaveStreamTest") }" });
            var persistence = new MempoolPersistence(settings, new LoggerFactory());
            IEnumerable<MempoolPersistenceEntry> toSave = this.CreateTestEntries(numTx);
            List<MempoolPersistenceEntry> loaded;

            long actualStreamLength = 0;
            ulong actualVersion = 0;
            long actualCount = -1;
            using (var ms = new MemoryStream())
            {
                persistence.DumpToStream(settings.Network, toSave, ms);
                actualStreamLength = ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
                var bitcoinReader = new BitcoinStream(ms, false)
                {
                    ConsensusFactory = settings.Network.Consensus.ConsensusFactory
                };

                bitcoinReader.ReadWrite(ref actualVersion);
                bitcoinReader.ReadWrite(ref actualCount);

                loaded = new List<MempoolPersistenceEntry>();
                for (int i = 0; i < actualCount; i++)
                {
                    MempoolPersistenceEntry entry = default(MempoolPersistenceEntry);
                    bitcoinReader.ReadWrite(ref entry);
                    loaded.Add(entry);
                }
            }

            Assert.True(actualStreamLength > 0);
            Assert.Equal(MempoolPersistence.MempoolDumpVersion, actualVersion);
            Assert.Equal(numTx, actualCount);
            Assert.Equal(loaded, toSave.ToArray());
        }

        private NodeSettings CreateSettings(string subDirName)
        {
            return new NodeSettings(this.network, args: new string[] { $"-datadir={ Directory.CreateDirectory(Path.Combine(this.dir, subDirName)).FullName }" });
        }

        private IEnumerable<MempoolPersistenceEntry> CreateTestEntries(int numTx)
        {
            var entries = new List<TxMempoolEntry>(numTx);
            for (int i = 0; i < numTx; i++)
            {
                int amountSat = 10 * i;
                Transaction tx = this.MakeRandomTx(amountSat);
                var entry = new TxMempoolEntry(tx, Money.FromUnit(0.1m, MoneyUnit.MilliBTC), DateTimeOffset.Now.ToUnixTimeSeconds(), i * 100, i, amountSat, i == 0, 10, null, new ConsensusOptions());
                entry.UpdateFeeDelta(numTx - i);
                entries.Add(entry);
            }
            return entries.Select(entry => MempoolPersistenceEntry.FromTxMempoolEntry(entry));
        }

        private Transaction MakeRandomTx(int satAmount = 10)
        {
            Money amount = Money.FromUnit(satAmount, MoneyUnit.Satoshi);
            var trx = this.network.CreateTransaction();
            trx.AddInput(new TxIn(Script.Empty));
            trx.AddOutput(amount, RandomScript());
            trx.AddInput(new TxIn(Script.Empty));
            trx.AddOutput(amount, RandomScript());
            return trx;
        }

        private static Script RandomScript()
        {
            return new Script(Guid.NewGuid().ToByteArray()
                            .Concat(Guid.NewGuid().ToByteArray())
                            .Concat(Guid.NewGuid().ToByteArray())
                            .Concat(Guid.NewGuid().ToByteArray())
                            .Concat(Guid.NewGuid().ToByteArray())
                            .Concat(Guid.NewGuid().ToByteArray()));
        }

        private MempoolManager CreateTestMempool(NodeSettings settings, out TxMempool txMemPool)
        {
            var mempoolSettings = new MempoolSettings(settings);
            IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;
            NodeSettings nodeSettings = NodeSettings.Default(settings.Network);
            ILoggerFactory loggerFactory = nodeSettings.LoggerFactory;
            var consensusSettings = new ConsensusSettings(nodeSettings);
            txMemPool = new TxMempool(dateTimeProvider, new BlockPolicyEstimator(new MempoolSettings(nodeSettings), loggerFactory, nodeSettings), loggerFactory, nodeSettings);
            var mempoolLock = new MempoolSchedulerLock();
            var coins = new InMemoryCoinView(settings.Network.GenesisHash);
            var chain = new ConcurrentChain(settings.Network);
            var chainState = new ChainState();
            var mempoolPersistence = new MempoolPersistence(settings, loggerFactory);
            this.network.Consensus.Options = new PosConsensusOptions();
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(this.network.Consensus);
            ConsensusRuleEngine consensusRules = new PowConsensusRuleEngine(this.network, loggerFactory, dateTimeProvider, chain, new NodeDeployments(this.network, chain),
                consensusSettings, new Checkpoints(), coins, chainState, new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider)).Register();
            var mempoolValidator = new MempoolValidator(txMemPool, mempoolLock, dateTimeProvider, mempoolSettings, chain, coins, loggerFactory, settings, consensusRules);
            return new MempoolManager(mempoolLock, txMemPool, mempoolValidator, dateTimeProvider, mempoolSettings, mempoolPersistence, coins, loggerFactory, settings.Network);
        }
    }
}