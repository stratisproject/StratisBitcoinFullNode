using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Trie
{
    public class MemoryDictionarySource : ISource<byte[], byte[]>
    {
        private Dictionary<byte[], byte[]> db;

        public MemoryDictionarySource()
        {
            this.db = new Dictionary<byte[], byte[]>();
        }

        public void Delete(byte[] key)
        {
            this.db.Remove(key);
        }

        public bool Flush()
        {
            throw new NotImplementedException();
        }

        public byte[] Get(byte[] key)
        {
            return this.db[key];
        }

        public void Put(byte[] key, byte[] val)
        {
            this.db[key] = val;
        }
    }
}
