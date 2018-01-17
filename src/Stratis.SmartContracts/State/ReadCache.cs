using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class ReadCache<Value> : AbstractCachedSource<byte[], Value>
    {
        private Dictionary<byte[], Value> cache;
        private bool byteKeyMap;

        public ReadCache(ISource<byte[], Value> src) : base(src)
        {
            WithCache(new Dictionary<byte[], Value>(new ByteArrayComparer()));
        }

        public ReadCache<Value> WithCache(Dictionary<byte[], Value> cache)
        {
            // EthereumJ does something with synchronising here
            this.cache = cache;
            return this;
        }

        public ReadCache<Value> withMaxCapacity(int maxCapacity)
        {
            throw new NotImplementedException(); // Shouldn't need surely.
        }

        public override void Put(byte[] key, Value val)
        {
            if (val == null)
            {
                Delete(key);
            }
            else
            {
                cache[key] = val;
                CacheAdded(key, val);
                GetSource().Put(key, val);
            }
        }

        public override Value Get(byte[] key)
        {
            Value ret = default(Value);
            if (cache.ContainsKey(key))
                ret = cache[key];

            if (ret == null)
            {
                ret = GetSource().Get(key);
                if (ret != null)
                    cache[key] = ret;
                CacheAdded(key, ret);
            }
            return ret;
        }

        public override void Delete(byte[] key)
        {
            Value value = cache[key];
            cache.Remove(key);
            CacheRemoved(key, value);
            GetSource().Delete(key);
        }

        protected override bool FlushImpl()
        {
            return false;
        }

        public override ICollection<byte[]> GetModified() {
            return new List<byte[]>();
        }

        public override bool HasModified()
        {
            return false;
        }

        public override Entry<Value> GetCached(byte[] key) {
            Value value = default(Value);
            if (cache.ContainsKey(key))
                value = cache[key];
            return value == null ? null : new SimpleEntry<Value>(value);
        }
        
    }
}
