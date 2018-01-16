using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class ReadWriteCache<Key, Value> : SourceChainBox<Key, Value, Key, Value>, ICachedSource<Key, Value>
    {

        protected ReadCache<Key, Value> readCache;
        protected WriteCache<Key, Value> writeCache;

        protected ReadWriteCache(ISource<Key, Value> source) : base(source)
        {
        }

        public ReadWriteCache(ISource<Key, Value> src, WriteCache<Key, Value>.CacheType cacheType) : base(src)
        {
            Add(writeCache = new WriteCache<Key, Value>(src, cacheType));
            Add(readCache = new ReadCache<Key, Value>(writeCache));
            readCache.SetFlushSource(true);
        }

        public ICollection<Key> GetModified()
        {
            return writeCache.GetModified();
        }

        public bool HasModified()
        {
            return writeCache.HasModified();
        }

        protected AbstractCachedSource<Key, Value>.Entry<Value> GetCached(Key key)
        {
            AbstractCachedSource<Key, Value>.Entry<Value> v = readCache.GetCached(key);
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
