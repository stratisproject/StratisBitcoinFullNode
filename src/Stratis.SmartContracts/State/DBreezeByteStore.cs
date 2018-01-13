using System;
using System.Collections.Generic;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.SmartContracts.Trie;

namespace Stratis.SmartContracts.State
{
    public class DBreezeByteStore : ISource<byte[], byte[]>
    {
        private DBreezeEngine engine;
        private string table;

        public DBreezeByteStore(DBreezeEngine engine, string table)
        {
            this.engine = engine;
            this.table = table;
        }

        public byte[] Get(byte[] key)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                Row<byte[], byte[]> row = t.Select<byte[], byte[]>(this.table.ToString(), key);

                if (row.Exists)
                    return row.Value;

                return null;
            }
        }

        public void Put(byte[] key, byte[] val)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.Insert(this.table.ToString(), key, val);
                t.Commit();
            }
        }

        public void Delete(byte[] key)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.RemoveKey(this.table.ToString(), key);
                t.Commit();
            }
        }

        public bool Flush()
        {
            throw new NotImplementedException("Can't flush - no underlying DB");
        }
    }
}
