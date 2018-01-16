using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public abstract class MultiCache<V> : ReadWriteCache<byte[], V> where V : ICachedSource<byte[], byte[]>
    {

        public MultiCache(ICachedSource<byte[], V> src) : base(src, WriteCache<byte[], V>.CacheType.SIMPLE)
        {
        }

        public override V Get(byte[] key)
        {
            AbstractCachedSource<byte[], V>.Entry<V> ownCacheEntry = GetCached(key);
            V ownCache = ownCacheEntry == null ? default(V) : ownCacheEntry.Value();
            if (ownCache == null)
            {
                V v = GetSource() != null ? base.Get(key) : default(V);
                ownCache = Create(key, v);
                Put(key, ownCache);
            }
            return ownCache;
        }

        protected override bool FlushImpl()
        {
            bool ret = false;
            foreach (byte[] key in writeCache.GetModified())
            {
                V value = base.Get(key);
                if (value == null)
                {
                    // cache was deleted
                    ret |= FlushChild(key, value);
                    if (GetSource() != null)
                    {
                        GetSource().Delete(key);
                    }
                }
                else if (value.GetSource() != null)
                {
                    ret |= FlushChild(key, value);
                }
                else
                {
                    GetSource().Put(key, value);
                    ret = true;
                }
            }
            return ret;
        }

        protected bool FlushChild(byte[] key, V childCache)
        {
            return childCache != null ? childCache.Flush() : true;
        }

        protected abstract V Create(byte[] key, V srcCache);
    }

    public class RealMultiCache : MultiCache<ICachedSource<byte[], byte[]>>
    {
        public RealMultiCache(ICachedSource<byte[], ICachedSource<byte[], byte[]>> src) : base(src)
        {
        }

        protected override ICachedSource<byte[], byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
        {
            return new WriteCache<byte[], byte[]>(srcCache, WriteCache<byte[], byte[]>.CacheType.SIMPLE);
        }
    }
}
