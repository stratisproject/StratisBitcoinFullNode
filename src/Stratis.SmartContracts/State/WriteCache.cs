using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{

    // I already know there's going to be issues here with retrieving keys. Just need to go through and be consistent

    public class WriteCache<Value> : AbstractCachedSource<byte[], Value>
    {
        public enum CacheType
        {
            SIMPLE,
            COUNTING
        }

        public abstract class CacheEntry<V> : Entry<V>
        {
            // dedicated value instance which indicates that the entry was deleted
            // (ref counter decremented) but we don't know actual value behind it
            public static object UNKNOWN_VALUE = new object();

            public V value;
            public int counter = 0;

            protected CacheEntry(V value)
            {
                this.value = value;
            }

            public abstract void Deleted();

            public abstract void Added();

            public abstract V GetValue();

            public V Value()
            {
                V v = GetValue();
                return v;
            }
        }

        public class SimpleCacheEntry<V> : CacheEntry<V>
        {

            public SimpleCacheEntry(V value) : base(value)
            {
            }

            public override void Deleted()
            {
                counter = -1;
            }

            public override void Added()
            {
                counter = 1;
            }

            public override V GetValue()
            {
                return counter < 0 ? default(V) : value;
            }
        }

        public class CountCacheEntry<V> : CacheEntry<V>
        {

            public CountCacheEntry(V value) : base(value)
            {
            }

            public override void Deleted()
            {
                counter--;
            }

            public override void Added()
            {
                counter++;
            }

            public override V GetValue()
            {
                return value;
            }
        }

        private bool isCounting;

        protected volatile Dictionary<byte[], CacheEntry<Value>> cache = new Dictionary<byte[], CacheEntry<Value>>(new ByteArrayComparer());

        protected object readLock = new object();
        protected object writeLock = new object();
        protected object updateLock = new object();

        //protected ReadWriteUpdateLock rwuLock = new ReentrantReadWriteUpdateLock();
        //protected ALock readLock = new ALock(rwuLock.readLock());
        //protected ALock writeLock = new ALock(rwuLock.writeLock());
        //protected ALock updateLock = new ALock(rwuLock.updateLock());


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
            lock (readLock)
            {
                return cache.Keys;
            }
        }


        public override bool HasModified()
        {
            return cache.Keys.Count > 0;
        }

        private CacheEntry<Value> CreateCacheEntry(Value val)
        {
            if (isCounting)
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
                Delete(key);
                return;
            }
            lock (writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (cache.ContainsKey(key))
                    curVal = cache[key];
                if (curVal == null)
                {
                    curVal = CreateCacheEntry(val);
                    CacheEntry<Value> oldVal = null;
                    if (cache.ContainsKey(key))
                        oldVal = cache[key];
                    cache[key] = curVal;
                    if (oldVal != null)
                    {
                        CacheRemoved(key, oldVal.value);
                    }
                    CacheAdded(key, curVal.value);
                }
                // assigning for non-counting cache only
                // for counting cache the value should be immutable (see HashedKeySource)
                curVal.value = val;
                curVal.Added();
            }
        }

        public override Value Get(byte[] key)
        {
            lock (readLock)
            {
                CacheEntry<Value> curVal = null;
                if (cache.ContainsKey(key))
                    curVal = cache[key];
                if (curVal == null)
                {
                    return GetSource() == null ? default(Value) : GetSource().Get(key);
                }
                else
                {
                    Value value = curVal.GetValue();
                    if (value == null) // no idea
                    {
                        return GetSource() == null ? default(Value) : GetSource().Get(key);
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
            lock (writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (cache.ContainsKey(key))
                    curVal = cache[key];
                if (curVal == null)
                {
                    curVal = CreateCacheEntry(default(Value));
                    CacheEntry<Value> oldVal = null;
                    if (cache.ContainsKey(key))
                        oldVal = cache[key];
                    cache[key] = curVal;
                    if (oldVal != null)
                    {
                        CacheRemoved(key, oldVal.value);
                    }
                    CacheAdded(key, curVal.value);
                }
                curVal.Deleted();
            }
        }

        public override bool Flush()
        {
            bool ret = false;
            lock (updateLock)
            {
                foreach (var entry in cache)
                {
                    if (entry.Value.counter > 0)
                    {
                        for (int i = 0; i < entry.Value.counter; i++)
                        {
                            GetSource().Put(entry.Key, entry.Value.value);
                        }
                        ret = true;
                    }
                    else if (entry.Value.counter < 0)
                    {
                        for (int i = 0; i > entry.Value.counter; i--)
                        {
                            GetSource().Delete(entry.Key);
                        }
                        ret = true;
                    }
                }
                if (flushSource)
                {
                    GetSource().Flush();
                }
                lock (writeLock)
                {
                    cache.Clear();
                    CacheCleared();
                }
                return ret;
            }
        }

        protected override bool FlushImpl()
        {
            return false;
        }

        public override Entry<Value> GetCached(byte[] key)
        {
            lock (readLock)
            {
                CacheEntry<Value> entry = null;
                if (cache.ContainsKey(key))
                    entry = cache[key];
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

        public long DebugCacheSize()
        {
            long ret = 0;
            foreach (var entry in cache)
            {
                ret += keySizeEstimator.EstimateSize(entry.Key);
                ret += valueSizeEstimator.EstimateSize(entry.Value.Value());
            }
            return ret;
        }
    }
}
