using System;
using System.Collections.Generic;
using System.Text;
using DBreeze;
using NBitcoin;
using DBreeze.DataTypes;
using Stratis.SmartContracts.Trie;

namespace Stratis.SmartContracts.State
{
    public class ContractStorageDb : ISource<byte[], byte[]>
    {
        private DBreezeEngine engine;
        private uint160 address;

        public ContractStorageDb(DBreezeEngine engine, uint160 address)
        {
            this.engine = engine;
            this.address = address;
        }

        public byte[] Get(byte[] key)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                Row<byte[], byte[]> row = t.Select<byte[], byte[]>(this.address.ToString(), key);

                if (row.Exists)
                    return row.Value;

                return null;
            }
        }

        public void Put(byte[] key, byte[] val)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.Insert(this.address.ToString(), key, val);
                t.Commit();
            }
        }

        public void Delete(byte[] key)
        {
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.RemoveKey(this.address.ToString(), key);
                t.Commit();
            }
        }

        public bool Flush()
        {
            throw new NotImplementedException("Can't flush - no underlying DB");
        }
    }

    //public class CodeStorageDb
    //{
    //    // Saves code and hashes to dbreeze
    //}

    //public class AccountStateDb : ISource<byte, byte>
    //{
    //    //Saves account state to db

    //    public void Delete(byte key)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool Flush()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public byte Get(byte key)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void Put(byte key, byte val)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
