using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class ReadWriteCache<Value> : SourceChainBox<byte[], Value, byte[], Value>, ICachedSource<byte[], Value>
    {

        protected ReadCache<Value> readCache;
        protected WriteCache<Value> writeCache;

        protected ReadWriteCache(ISource<byte[], Value> source) : base(source)
        {
        }

        public ReadWriteCache(ISource<byte[], Value> src, WriteCache<Value>.CacheType cacheType) : base(src)
        {
            Add(writeCache = new WriteCache<Value>(src, cacheType));
            Add(readCache = new ReadCache<Value>(writeCache));
            readCache.SetFlushSource(true);
        }

        public ICollection<byte[]> GetModified()
        {
            return writeCache.GetModified();
        }

        public bool HasModified()
        {
            return writeCache.HasModified();
        }

        protected AbstractCachedSource<byte[], Value>.Entry<Value> GetCached(byte[] key)
        {
            AbstractCachedSource<byte[], Value>.Entry<Value> v = readCache.GetCached(key);
            if (v == null)
            {
                v = writeCache.GetCached(key);
            }
            return v;
        }

        public long EstimateCacheSize()
        {
            return readCache.EstimateCacheSize() + writeCache.EstimateCacheSize();
        }
    }
}
