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
        public void SaveToStreamTest()
        {
            int numTx = 22;
            int expectedLinesPerTransaction = 3;
            int expectedHeaderLines = 2;
            int expectedLines = numTx * expectedLinesPerTransaction + expectedHeaderLines;
            MempoolPersistence persistence = new MempoolPersistence(this.settings);
            IEnumerable<MempoolPersistenceEntry> toSave = CreateTestEntries(numTx);

            long actualStreamLength = 0;
            ulong actualVersion = 0;
            int actualCount = -1;
            using (MemoryStream ms = new MemoryStream())
            {
                var bitcoinWriter = new BitcoinStream(ms, true);
                persistence.DumpToStream(toSave, bitcoinWriter);
                actualStreamLength = ms.Length;
                ms.Seek(0, SeekOrigin.Begin);
                var bitcoinReader = new BitcoinStream(ms, false);

                bitcoinReader.ReadWrite(ref actualVersion);
                bitcoinReader.ReadWrite(ref actualCount);
            }

            Assert.True(actualStreamLength > 0);
            Assert.Equal(MempoolPersistence.MEMPOOL_DUMP_VERSION, actualVersion);
            Assert.Equal(numTx, actualCount);
        }

        private IEnumerable<MempoolPersistenceEntry> CreateTestEntries(int numTx)
        {
            var entries = new List<TxMempoolEntry>(numTx);
            for (int i = 0; i < numTx; i++)
            {
                var amountSat = 10 * i;
                Transaction tx = MakeRandomTx(amountSat);
                entries.Add(new TxMempoolEntry(tx, Money.FromUnit(0.1m, MoneyUnit.MilliBTC), DateTimeOffset.Now.ToUnixTimeSeconds(), i * 100, i, amountSat, i == 0, 10, null));
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
