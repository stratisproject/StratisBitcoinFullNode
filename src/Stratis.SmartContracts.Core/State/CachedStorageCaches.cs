using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Created for each new level of tracking. Introduces a WriteCache in front of the StorageCache itself.
    /// TODO: Rename
    /// </summary>
    public class CachedStorageCaches : IStorageCaches
    {
        /// <summary>
        /// The previous level of storage caching. May be the RootStorageCache where a StorageCache is spawned.
        /// </summary>
        private readonly IStorageCaches previous;

        private readonly Dictionary<byte[], ISource<byte[], byte[]>> cache;

        public CachedStorageCaches(IStorageCaches previous)
        {
            this.previous = previous;
            this.cache = new Dictionary<byte[], ISource<byte[], byte[]>>(new ByteArrayComparer());

        }

        /// <inheritdoc />
        public ISource<byte[],byte[]> Get(byte[] key)
        {
            if (this.cache.ContainsKey(key))
                return this.cache[key];

            ISource<byte[], byte[]> newStorage = this.previous.Get(key);
            this.cache[key] = new WriteCache<byte[]>(newStorage, WriteCache<byte[]>.CacheType.SIMPLE);
            return this.cache[key];
        }

        /// <inheritdoc />
        public bool Flush()
        {
            // TODO: May be able to introduce a GetModified() like in other parts of State as an optimisation
            bool ret = false;
            foreach(KeyValuePair<byte[], ISource<byte[], byte[]>> source in this.cache)
            {
                ret |= source.Value.Flush();
            }

            this.cache.Clear();

            return ret;
        }
    }
}
