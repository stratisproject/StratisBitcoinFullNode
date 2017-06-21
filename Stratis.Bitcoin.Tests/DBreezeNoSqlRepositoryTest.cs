using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{
    [TestClass]
    public class DBreezeNoSqlRepositoryTest : TestBase
    {
        [TestMethod]
        public void GetBytesWithKeyRetrievesBytesForExistingGivenKey()
        {
            var dir = AssureEmptyDir("TestData/DBreezeNoSqlRepository/GetBytesWithKeyRetrievesBytesForExistingGivenKey");
            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert("TestRepo", "testKey", Encoding.UTF8.GetBytes("keyValueResult"));
                transaction.Commit();
            }

            using (var repo = new DBreezeTestNoSqlRepository("TestRepo", dir))
            {
                var task = repo.GetBytes("testKey");
                task.Wait();

                Assert.AreEqual("keyValueResult", Encoding.UTF8.GetString(task.Result));
            }
        }

        [TestMethod]
        public void GetBytesWithKeyRetrievesNullWhenKeyNotExists()
        {
            using (var repo = new DBreezeTestNoSqlRepository("TestRepo", AssureEmptyDir("TestData/DBreezeNoSqlRepository/GetBytesWithKeyRetrievesNullWhenKeyNotExists")))
            {
                var task = repo.GetBytes("testKey");
                task.Wait();

                Assert.AreEqual(null, task.Result);
            }
        }

        [TestMethod]
        public void PutBytesWithKeyAndDataStoresDataUnderGivenKey()
        {
            var dir = AssureEmptyDir("TestData/DBreezeNoSqlRepository/PutBytesWithKeyAndDataStoresDataUnderGivenKey");

            var expected = Encoding.UTF8.GetBytes("keyData");
            using (var repo = new DBreezeTestNoSqlRepository("TestRepo2", dir))
            {
                var task = repo.PutBytes("dataKey", expected);
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                var row = transaction.Select<string, byte[]>("TestRepo2", "dataKey");
                Assert.IsTrue(expected.SequenceEqual(row.Value));
            }
        }

        [TestMethod]
        public void PutBytesBatchWithListStoresDataUnderGivenKeys()
        {
            var dir = AssureEmptyDir("TestData/DBreezeNoSqlRepository/PutBytesBatchWithListStoresDataUnderGivenKeys");

            var expected = new List<Tuple<string, byte[]>>();
            expected.Add(new Tuple<string, byte[]>("testBatchKey1", new byte[] { 1, 5, 6, 7 }));
            expected.Add(new Tuple<string, byte[]>("testBatchKey2", new byte[] { 8, 3, 2, 1 }));

            using (var repo = new DBreezeTestNoSqlRepository("TestRepo3", dir))
            {
                var task = repo.PutBytesBatch(expected);
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                var row = transaction.Select<string, byte[]>("TestRepo3", expected[0].Item1);
                Assert.IsTrue(expected[0].Item2.SequenceEqual(row.Value));

                row = transaction.Select<string, byte[]>("TestRepo3", expected[1].Item1);
                Assert.IsTrue(expected[1].Item2.SequenceEqual(row.Value));
            }
        }

        private class DBreezeTestNoSqlRepository : DBreezeNoSqlRepository
        {
            public DBreezeTestNoSqlRepository(string name, string folder) : base(name, folder)
            {
            }

            public new Task<byte[]> GetBytes(string key)
            {
                return base.GetBytes(key);
            }

            public new Task PutBytes(string key, byte[] data)
            {
                return base.PutBytes(key, data);
            }

            public new Task PutBytesBatch(IEnumerable<Tuple<string, byte[]>> enumerable)
            {
                return base.PutBytesBatch(enumerable);
            }
        }
    }
}
