using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// Caches values in memory and flushes to another source.
    /// </summary>
    public class WriteCache<Value> : AbstractCachedSource<byte[], Value>
    {
        public enum CacheType
        {
            SIMPLE,
            COUNTING
        }

        public abstract class CacheEntry<V> : IEntry<V>
        {
            public V value; 

            public int counter = 0;

            protected CacheEntry(V value)
            {
                this.value = value;
            }

            public V Value()
            {
                return this.GetValue();
            }

            public abstract void Deleted();

            public abstract void Added();

            public abstract V GetValue();
        }

        public class SimpleCacheEntry<V> : CacheEntry<V>
        {

            public SimpleCacheEntry(V value) : base(value)
            {
            }

            public override void Deleted()
            {
                this.counter = -1;
            }

            public override void Added()
            {
                this.counter = 1;
            }

            public override V GetValue()
            {
                return this.counter < 0 ? default(V) : this.value;
            }
        }

        public class CountCacheEntry<V> : CacheEntry<V>
        {

            public CountCacheEntry(V value) : base(value)
            {
            }

            public override void Deleted()
            {
                this.counter--;
            }

            public override void Added()
            {
                this.counter++;
            }

            public override V GetValue()
            {
                return this.value;
            }
        }

        private bool isCounting;

        protected volatile Dictionary<byte[], CacheEntry<Value>> cache = new Dictionary<byte[], CacheEntry<Value>>(new ByteArrayComparer());

        protected object readLock = new object();
        protected object writeLock = new object();
        protected object updateLock = new object();

        public WriteCache(ISource<byte[], Value> src, CacheType cacheType) : base(src)
        {
            this.isCounting = cacheType == CacheType.COUNTING;
        }

        public WriteCache<Value> WithCache(Dictionary<byte[], CacheEntry<Value>> cache)
        {
            this.cache = cache;
            return this;
        }

        public override ICollection<byte[]> GetModified()
        {
            lock (this.readLock)
            {
                return this.cache.Keys;
            }
        }


        public override bool HasModified()
        {
            return this.cache.Keys.Count > 0;
        }

        private CacheEntry<Value> CreateCacheEntry(Value val)
        {
            if (this.isCounting)
            {
                return new CountCacheEntry<Value>(val);
            }
            else
            {
                return new SimpleCacheEntry<Value>(val);
            }
        }

        public override void Put(byte[] key, Value val)
        {
            if (val == null)
            {
                this.Delete(key);
                return;
            }
            lock (this.writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (this.cache.ContainsKey(key))
                    curVal = this.cache[key];
                if (curVal == null)
                {
                    curVal = this.CreateCacheEntry(val);
                    CacheEntry<Value> oldVal = null;
                    if (this.cache.ContainsKey(key))
                        oldVal = this.cache[key];
                    this.cache[key] = curVal;
                }
                // assigning for non-counting cache only
                // for counting cache the value should be immutable (see HashedKeySource)
                curVal.value = val;
                curVal.Added();
            }
        }

        public override Value Get(byte[] key)
        {
            lock (this.readLock)
            {
                CacheEntry<Value> curVal = null;
                if (this.cache.ContainsKey(key))
                    curVal = this.cache[key];
                if (curVal == null)
                {
                    return this.Source == null ? default(Value) : this.Source.Get(key);
                }
                else
                {
                    Value value = curVal.GetValue();
                    if (value == null) // no idea
                    {
                        return this.Source == null ? default(Value) : this.Source.Get(key);
                    }
                    else
                    {
                        return value;
                    }
                }
            }

        }

        public override void Delete(byte[] key)
        {
            lock (this.writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (this.cache.ContainsKey(key))
                    curVal = this.cache[key];
                if (curVal == null)
                {
                    curVal = this.CreateCacheEntry(default(Value));
                    CacheEntry<Value> oldVal = null;
                    if (this.cache.ContainsKey(key))
                        oldVal = this.cache[key];
                    this.cache[key] = curVal;
                }
                curVal.Deleted();
            }
        }

        public override bool Flush()
        {
            bool ret = false;
            lock (this.updateLock)
            {
                foreach (KeyValuePair<byte[], CacheEntry<Value>> entry in this.cache)
                {
                    if (entry.Value.counter > 0)
                    {
                        for (int i = 0; i < entry.Value.counter; i++)
                        {
                            this.Source.Put(entry.Key, entry.Value.value);
                        }
                        ret = true;
                    }
                    else if (entry.Value.counter < 0)
                    {
                        for (int i = 0; i > entry.Value.counter; i--)
                        {
                            this.Source.Delete(entry.Key);
                        }
                        ret = true;
                    }
                }
                if (this.flushSource)
                {
                    this.Source.Flush();
                }
                lock (this.writeLock)
                {
                    this.cache.Clear();
                }
                return ret;
            }
        }

        protected override bool FlushImpl()
        {
            return false;
        }

        public override IEntry<Value> GetCached(byte[] key)
        {
            lock (this.readLock)
            {
                CacheEntry<Value> entry = null;
                if (this.cache.ContainsKey(key))
                    entry = this.cache[key];
                if (entry == null)
                {
                    return null;
                }
                else
                {
                    return entry;
                }
            }
        }
    }
}
