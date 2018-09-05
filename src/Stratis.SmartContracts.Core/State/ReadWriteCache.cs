using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// Wrapper for situations when you wish to cache the items being read and cache the values being set.
    /// </summary>
    public class ReadWriteCache<Value> : SourceChainBox<byte[], Value, byte[], Value>, ICachedSource<byte[], Value>
    {
        protected ReadCache<Value> readCache;
        protected WriteCache<Value> writeCache;

        protected ReadWriteCache(ISource<byte[], Value> source) : base(source)
        {
        }

        public ReadWriteCache(ISource<byte[], Value> src, WriteCache<Value>.CacheType cacheType) : base(src)
        {
            this.Add(this.writeCache = new WriteCache<Value>(src, cacheType));
            this.Add(this.readCache = new ReadCache<Value>(this.writeCache));
            this.readCache.SetFlushSource(true);
        }

        public ICollection<byte[]> GetModified()
        {
            return this.writeCache.GetModified();
        }

        public bool HasModified()
        {
            return this.writeCache.HasModified();
        }

        protected AbstractCachedSource<byte[], Value>.IEntry<Value> GetCached(byte[] key)
        {
            AbstractCachedSource<byte[], Value>.IEntry<Value> v = this.readCache.GetCached(key);
            if (v == null)
            {
                v = this.writeCache.GetCached(key);
            }
            return v;
        }
    }
}
