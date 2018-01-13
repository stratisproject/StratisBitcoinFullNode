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

        public class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] left, byte[] right)
            {
                if (left == null || right == null)
                {
                    return left == right;
                }
                if (left.Length != right.Length)
                {
                    return false;
                }
                for (int i = 0; i < left.Length; i++)
                {
                    if (left[i] != right[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            public int GetHashCode(byte[] key)
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                int sum = 0;
                foreach (byte cur in key)
                {
                    sum += cur;
                }
                return sum;
            }
        }

    }
}
