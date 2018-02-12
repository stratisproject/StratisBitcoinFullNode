using System;
using System.Collections.Generic;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// A basic Key/Value store in DBreeze.
    /// </summary>
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
                Dictionary<byte[], byte[]> test = t.SelectDictionary<byte[], byte[]>(this.table);

                Row<byte[], byte[]> row = t.Select<byte[], byte[]>(this.table, key);

                if (row.Exists)
                    return row.Value;

                return null;
            }
        }

        public void Put(byte[] key, byte[] val)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.Insert(this.table, key, val);
                t.Commit();
            }
        }

        public void Delete(byte[] key)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.RemoveKey(this.table, key);
                t.Commit();
            }
        }

        public bool Flush()
        {
            throw new NotImplementedException("Can't flush - no underlying DB");
        }

        /// <summary>
        /// Only use for testing at the moment.
        /// </summary>
        internal void Empty()
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.RemoveAllKeys(this.table, false);
                t.Commit();
            }
        }
    }
}
