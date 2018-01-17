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

        public abstract class CacheEntry<V> : IEntry<V>
        {
            // dedicated value instance which indicates that the entry was deleted
            // (ref counter decremented) but we don't know actual value behind it
            public static object UNKNOWN_VALUE = new object();

            public V Value { get; set; }
            public int counter = 0;

            protected CacheEntry(V value)
            {
                this.Value = value;
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
                return this.counter < 0 ? default(V) : this.Value;
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
                return this.Value;
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
                Delete(key);
                return;
            }
            lock (this.writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (this.cache.ContainsKey(key))
                    curVal = this.cache[key];
                if (curVal == null)
                {
                    curVal = CreateCacheEntry(val);
                    CacheEntry<Value> oldVal = null;
                    if (this.cache.ContainsKey(key))
                        oldVal = this.cache[key];
                    this.cache[key] = curVal;
                    if (oldVal != null)
                    {
                        CacheRemoved(key, oldVal.Value);
                    }
                    CacheAdded(key, curVal.Value);
                }
                // assigning for non-counting cache only
                // for counting cache the value should be immutable (see HashedKeySource)
                curVal.Value = val;
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
            lock (this.writeLock)
            {
                CacheEntry<Value> curVal = null;
                if (this.cache.ContainsKey(key))
                    curVal = this.cache[key];
                if (curVal == null)
                {
                    curVal = CreateCacheEntry(default(Value));
                    CacheEntry<Value> oldVal = null;
                    if (this.cache.ContainsKey(key))
                        oldVal = this.cache[key];
                    this.cache[key] = curVal;
                    if (oldVal != null)
                    {
                        CacheRemoved(key, oldVal.Value);
                    }
                    CacheAdded(key, curVal.Value);
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
                            GetSource().Put(entry.Key, entry.Value.Value);
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
                if (this.flushSource)
                {
                    GetSource().Flush();
                }
                lock (this.writeLock)
                {
                    this.cache.Clear();
                    CacheCleared();
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

        public long DebugCacheSize()
        {
            long ret = 0;
            foreach (KeyValuePair<byte[], CacheEntry<Value>> entry in this.cache)
            {
                ret += this.keySizeEstimator.EstimateSize(entry.Key);
                ret += this.valueSizeEstimator.EstimateSize(entry.Value.Value);
            }
            return ret;
        }
    }
}
