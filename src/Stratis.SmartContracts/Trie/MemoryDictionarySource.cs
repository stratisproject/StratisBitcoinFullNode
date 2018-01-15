using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Trie
{
    public class MemoryDictionarySource : ISource<byte[], byte[]>
    {
        public Dictionary<byte[], byte[]> Db { get; private set; }

        public MemoryDictionarySource()
        {
            this.Db = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
        }

        public void Delete(byte[] key)
        {
            this.Db.Remove(key);
        }

        public bool Flush()
        {
            throw new NotImplementedException();
        }

        public byte[] Get(byte[] key)
        {
            return this.Db[key];
        }

        public void Put(byte[] key, byte[] val)
        {
            this.Db[key] = val;
        }
    }
}
