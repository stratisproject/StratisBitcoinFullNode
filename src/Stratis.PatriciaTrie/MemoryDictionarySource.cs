using System;
using System.Collections.Generic;

namespace Stratis.Patricia
{
    /// <summary>
    /// A basic in-memory database.
    /// </summary>
    public class MemoryDictionarySource : ISource<byte[], byte[]>
    {
        public Dictionary<byte[], byte[]> Db { get; }

        public MemoryDictionarySource()
        {
            this.Db = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
        }

        /// <inheritdoc />
        public void Delete(byte[] key)
        {
            this.Db.Remove(key);
        }

        /// <summary>
        /// Not implemented on MemoryDictionarySource.
        /// </summary>
        /// <returns></returns>
        public bool Flush()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public byte[] Get(byte[] key)
        {
            if (this.Db.ContainsKey(key))
                return this.Db[key];
            return null;
        }

        /// <inheritdoc />
        public void Put(byte[] key, byte[] val)
        {
            this.Db[key] = val;
        }
    }
}
