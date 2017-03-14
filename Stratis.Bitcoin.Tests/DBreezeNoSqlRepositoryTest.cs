using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class DBreezeNoSqlRepositoryTest : TestBase
    {
        public DBreezeNoSqlRepositoryTest()
        {
        }

        [Fact]
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

                Assert.Equal("keyValueResult", Encoding.UTF8.GetString(task.Result));
            }
        }

        [Fact]
        public void GetBytesWithKeyRetrievesNullWhenKeyNotExists()
        {
            using (var repo = new DBreezeTestNoSqlRepository("TestRepo", AssureEmptyDir("TestData/DBreezeNoSqlRepository/GetBytesWithKeyRetrievesNullWhenKeyNotExists")))
            {
                var task = repo.GetBytes("testKey");
                task.Wait();

                Assert.Equal(null, task.Result);
            }
        }

        [Fact]
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
                Assert.Equal(expected, row.Value);
            }
        }

        [Fact]
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
                Assert.Equal(expected[0].Item2, row.Value);

                row = transaction.Select<string, byte[]>("TestRepo3", expected[1].Item1);
                Assert.Equal(expected[1].Item2, row.Value);
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
