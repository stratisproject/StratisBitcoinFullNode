using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
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
    public class MempoolPersistenceTest
    {
        readonly NodeSettings settings;

        public MempoolPersistenceTest()
        {
            string dir = "Stratis.Bitcoin.Tests/TestData/MempoolPersistenceTest/";
            this.settings = NodeSettings.Default();
            this.settings.DataDir = dir;
        }

        [Fact]
        public void SaveLoadStreamTest()
        {
            int numTx = 1;
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
