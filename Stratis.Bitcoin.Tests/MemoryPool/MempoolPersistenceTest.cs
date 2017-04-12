using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Tests.MemoryPool
{
    public class MempoolPersistenceTest : IDisposable
    {
        private readonly bool shouldDeleteFolder = false;
        private readonly string dir;

        public MempoolPersistenceTest()
        {
            Logs.Configure(new LoggerFactory());
            this.dir = "Stratis.Bitcoin.Tests/TestData/MempoolPersistenceTest/";

            if (!Directory.Exists(this.dir))
            {
                this.shouldDeleteFolder = true;
                Directory.CreateDirectory(this.dir);
            }
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
            string fileName = @"mempool.dat";
            string dir = "Stratis.Bitcoin.Tests/TestData/MempoolPersistenceTest/";
            var settings = NodeSettings.Default();
            settings.DataDir = Directory.CreateDirectory(Path.Combine(dir, "SaveLoadFileTest")).FullName;
            MempoolPersistence persistence = new MempoolPersistence(settings);
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);
            IEnumerable<MempoolPersistenceEntry> loaded;

            MemPoolSaveResult result = persistence.Save(toSave, fileName);
            loaded = persistence.Load(fileName);

            Assert.True(File.Exists(Path.Combine(settings.DataDir, fileName)));
            Assert.True(result.Succeeded);
            Assert.Equal((uint)numTx, result.TrxSaved);
            Assert.Equal(loaded, toSave.ToArray());

        }

        [Fact]
        public void LoadPoolTest_WithBadTransactions()
        {
            int numTx = 5;
            string fileName = @"LoadPoolTest_WithBadTransactions_mempool.dat";
            var settings = NodeSettings.Default();
            settings.DataDir = Directory.CreateDirectory(Path.Combine(this.dir, "LoadPoolTest_WithBadTransactions")).FullName;
            var fullNodeBuilder = new FullNodeBuilder(settings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseMempool()
                .Build();

            MempoolManager mempoolManager = fullNode.Services.ServiceProvider.GetService<MempoolManager>();
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);

            MemPoolSaveResult result = (new MempoolPersistence(settings)).Save(toSave, fileName);
            mempoolManager.LoadPool(fileName).GetAwaiter().GetResult();
            long actualSize = mempoolManager.MempoolSize().GetAwaiter().GetResult();

            Assert.Equal(0, actualSize);

        }

        [Fact]
        public async Task LoadPoolTest_WithGoodTransactions()
        {
            string fileName = @"LoadPoolTest_WithGoodTransactions_mempool.dat";
            var settings = NodeSettings.Default();
            settings.DataDir = Directory.CreateDirectory(Path.Combine(this.dir, "LoadPoolTest_WithGoodTransactions")).FullName;
            var fullNodeBuilder = new FullNodeBuilder(settings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseMempool()
                .Build();

            MempoolManager mempoolManager = fullNode.Services.ServiceProvider.GetService<MempoolManager>();
            var tx1_input_orphan = new Transaction("0100000001c4fadb806f9679c27c30c11b694523f6ac9614f7a69076b8940082ce636040fb000000006b4830450221009ad4b969a40b95017d133b13f7d465031829731f3b0ae4bcdcb5e393f5e919f902207f33aad2c3af48d6d65aaf5dd15a85a1f588ee3d6f477b2236cda1d81d88c43b012102eb184a906e082db44a95347de64110952b5821c42068a2054947aec4bc60db2fffffffff02685e3e00000000001976a9149ed35c9c42543ec67f9e6d1033e2ac1ac76f86ba88acd33e4500000000001976a9143c88fada9101f660d77feec1dd8db4ee9ea01d6788ac00000000");
            var tx1 = new Transaction("0100000001055c4c42511f9d05f2fa817c7f023df567f3d501bebec14ddce7c05a9d5fda52000000006b483045022100de552f011768887141b9a767ae184f61aa3743a32aad394ac1e1ec35345415420220070b3d0afd28414f188c966e334e9f7b65e7440538d93bc1d61f82067fcfd3fa012103b47b6ffce08f54be286620a29f45407fedb7b33acfec938551938ec96a1e1b0bffffffff019f053e000000000017a91493e31884769545a237f164aa07b3caef6b62f6b68700000000");
            await mempoolManager.Orphans.AddOrphanTx(0, tx1_input_orphan);
            List<MempoolPersistenceEntry> toSave = new List<MempoolPersistenceEntry>
            {
                new MempoolPersistenceEntry{
                    Tx = tx1.ToBytes(),
                    Time=1491948625,
                    FeeDelta=10
                },
                //new MempoolPersistenceEntry{
                //    Tx = new Transaction("01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff30039d0a0700040620ed5804e6b8cc2d089d1beb58000405f9172f426974667572792f5345475749542f4249503134382fffffffff02e93a2652000000001976a914ab0fcc2fb04ee80d29a00a80140b16323bed3d6e88ac0000000000000000266a24aa21a9ed6481269d055522a05e0f2b26fde26ffc5635e4a59c4592767a32b125b562fba800000000").ToBytes(),
                //    Time=1491949656,
                //    FeeDelta=20
                //}
            };

            MemPoolSaveResult result = (new MempoolPersistence(settings)).Save(toSave, fileName);
            await mempoolManager.LoadPool(fileName);
            long actualSize = await mempoolManager.MempoolSize();

            Assert.Equal(2, actualSize);

        }


        [Fact]
        public void SaveStreamTest()
        {
            int numTx = 22;
            int expectedLinesPerTransaction = 3;
            int expectedHeaderLines = 2;
            int expectedLines = numTx * expectedLinesPerTransaction + expectedHeaderLines;
            var settings = NodeSettings.Default();
            settings.DataDir = Path.Combine(this.dir, "SaveStreamTest");
            MempoolPersistence persistence = new MempoolPersistence(settings);
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);
            List<MempoolPersistenceEntry> loaded;

            long actualStreamLength = 0;
            ulong actualVersion = 0;
            long actualCount = -1;
            using (MemoryStream ms = new MemoryStream())
            {
                persistence.DumpToStream(toSave, ms);
                actualStreamLength = ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
                var bitcoinReader = new BitcoinStream(ms, false);

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
            Assert.Equal(MempoolPersistence.MEMPOOL_DUMP_VERSION, actualVersion);
            Assert.Equal(numTx, actualCount);
            Assert.Equal(loaded, toSave.ToArray());
        }

        private IEnumerable<MempoolPersistenceEntry> CreateTestEntries(int numTx)
        {
            var entries = new List<TxMempoolEntry>(numTx);
            for (int i = 0; i < numTx; i++)
            {
                var amountSat = 10 * i;
                Transaction tx = MakeRandomTx(amountSat);
                var entry = new TxMempoolEntry(tx, Money.FromUnit(0.1m, MoneyUnit.MilliBTC), DateTimeOffset.Now.ToUnixTimeSeconds(), i * 100, i, amountSat, i == 0, 10, null);
                entry.UpdateFeeDelta(numTx - i);
                entries.Add(entry);
            }
            return entries.Select(entry => MempoolPersistenceEntry.FromTxMempoolEntry(entry));
        }

        private Transaction MakeRandomTx(int satAmount = 10)
        {
            var amount = Money.FromUnit(satAmount, MoneyUnit.Satoshi);
            var trx = new Transaction();
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
    }
}
