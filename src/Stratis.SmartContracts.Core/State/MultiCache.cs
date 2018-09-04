namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// This is a cache of caches.
    /// </summary>
    public abstract class MultiCacheBase<V> : ReadWriteCache<V> where V : ICachedSource<byte[], byte[]>
    {
        public MultiCacheBase(ICachedSource<byte[], V> src) : base(src, WriteCache<V>.CacheType.SIMPLE)
        {
        }

        public override V Get(byte[] key)
        {
            AbstractCachedSource<byte[], V>.IEntry<V> ownCacheEntry = this.GetCached(key);
            V ownCache = ownCacheEntry == null ? default(V) : ownCacheEntry.Value();
            if (ownCache == null)
            {
                V v = this.Source != null ? base.Get(key) : default(V);
                ownCache = this.Create(key, v);
                this.Put(key, ownCache);
            }
            return ownCache;
        }

        protected override bool FlushImpl()
        {
            bool ret = false;
            foreach (byte[] key in this.writeCache.GetModified())
            {
                V value = base.Get(key);
                if (value == null)
                {
                    // cache was deleted
                    ret |= this.FlushChild(key, value);
                    if (this.Source != null)
                    {
                        this.Source.Delete(key);
                    }
                }
                else if (value.Source != null)
                {
                    ret |= this.FlushChild(key, value);
                }
                else
                {
                    this.Source.Put(key, value);
                    ret = true;
                }
            }
            return ret;
        }

        protected virtual bool FlushChild(byte[] key, V childCache)
        {
            return childCache != null ? childCache.Flush() : true;
        }

        protected abstract V Create(byte[] key, V srcCache);
    }

    /// <summary>
    /// Implementation of a cache of caches.
    /// </summary>
    public class MultiCache : MultiCacheBase<ICachedSource<byte[], byte[]>>
    {
        public MultiCache(ICachedSource<byte[], ICachedSource<byte[], byte[]>> src) : base(src)
        {
        }

        protected override ICachedSource<byte[], byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
        {
            return new WriteCache<byte[]>(srcCache, WriteCache<byte[]>.CacheType.SIMPLE);
        }
    }
}
