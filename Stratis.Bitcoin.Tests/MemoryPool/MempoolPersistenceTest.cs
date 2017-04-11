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
        readonly NodeSettings settings;
        private readonly bool shouldDeleteFolder = false;

        public MempoolPersistenceTest()
        {
            Logs.Configure(new LoggerFactory());
            string dir = "Stratis.Bitcoin.Tests/TestData/MempoolPersistenceTest/";
            this.settings = NodeSettings.Default();
            this.settings.DataDir = dir;

            if (!Directory.Exists(dir))
            {
                this.shouldDeleteFolder = true;
                Directory.CreateDirectory(this.settings.DataDir);
            }
        }

        public void Dispose()
        {
            if (this.shouldDeleteFolder)
                Directory.Delete(this.settings.DataDir, true);
        }

        [Fact]
        public void SaveLoadFileTest()
        {
            int numTx = 22;
            string fileName = "SaveLoadFileTest_mempool.dat";
            MempoolPersistence persistence = new MempoolPersistence(this.settings);
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);
            IEnumerable<MempoolPersistenceEntry> loaded;

            MemPoolSaveResult result = persistence.Save(toSave, fileName);
            loaded = persistence.Load(fileName);

            Assert.True(File.Exists(Path.Combine(this.settings.DataDir, fileName)));
            Assert.True(result.Succeeded);
            Assert.Equal((uint)numTx, result.TrxSaved);
            Assert.Equal(loaded, toSave.ToArray());

        }

        [Fact]
        public async Task LoadPoolTest_WithBadTransactions()
        {
            int numTx = 5;
            string fileName = "LoadPoolTest_WithBadTransactions_mempool.dat";
            var fullNodeBuilder = new FullNodeBuilder(this.settings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseMempool()
                .Build();

            MempoolManager mempoolManager = fullNode.Services.ServiceProvider.GetService<MempoolManager>();
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);

            MemPoolSaveResult result = (new MempoolPersistence(this.settings)).Save(toSave, fileName);
            await mempoolManager.LoadPool(fileName);
            long actualSize = await mempoolManager.MempoolSize();

            Assert.Equal(0, actualSize);

        }

        //TODO: doesn't work. Find some valid transactions
        //[Fact]
        //public async Task LoadPoolTest_WithGoodTransactions()
        //{
        //    string fileName = "LoadPoolTest_WithGoodTransactions_mempool.dat";
        //    var fullNodeBuilder = new FullNodeBuilder(this.settings);
        //    using (IFullNode fullNode = fullNodeBuilder
        //        .UseConsensus()
        //        .UseMempool()
        //        .Build())
        //    {
        //        MempoolManager mempoolManager = fullNode.Services.ServiceProvider.GetService<MempoolManager>();
        //        List<MempoolPersistenceEntry> toSave = new List<MempoolPersistenceEntry>
        //    {
        //        new MempoolPersistenceEntry{
        //            Tx = new Transaction("0100000001055c4c42511f9d05f2fa817c7f023df567f3d501bebec14ddce7c05a9d5fda52000000006b483045022100de552f011768887141b9a767ae184f61aa3743a32aad394ac1e1ec35345415420220070b3d0afd28414f188c966e334e9f7b65e7440538d93bc1d61f82067fcfd3fa012103b47b6ffce08f54be286620a29f45407fedb7b33acfec938551938ec96a1e1b0bffffffff019f053e000000000017a91493e31884769545a237f164aa07b3caef6b62f6b68700000000").ToBytes(),
        //            Time=1491948625,
        //            FeeDelta=10
        //        },
        //        new MempoolPersistenceEntry{
        //            Tx = new Transaction("01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff30039d0a0700040620ed5804e6b8cc2d089d1beb58000405f9172f426974667572792f5345475749542f4249503134382fffffffff02e93a2652000000001976a914ab0fcc2fb04ee80d29a00a80140b16323bed3d6e88ac0000000000000000266a24aa21a9ed6481269d055522a05e0f2b26fde26ffc5635e4a59c4592767a32b125b562fba800000000").ToBytes(),
        //            Time=1491949656,
        //            FeeDelta=20
        //        }
        //    };

        //        MemPoolSaveResult result = (new MempoolPersistence(this.settings)).Save(toSave, fileName);
        //        await mempoolManager.LoadPool(fileName);
        //        long actualSize = await mempoolManager.MempoolSize();

        //        Assert.Equal(2, actualSize);
        //    }
        //}


        [Fact]
        public void SaveStreamTest()
        {
            int numTx = 22;
            int expectedLinesPerTransaction = 3;
            int expectedHeaderLines = 2;
            int expectedLines = numTx * expectedLinesPerTransaction + expectedHeaderLines;
            MempoolPersistence persistence = new MempoolPersistence(this.settings);
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
