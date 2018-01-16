using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class ReadCache<Value> : AbstractCachedSource<byte[], Value>
    {
        private Dictionary<Key, Value> cache;
        private bool byteKeyMap;

        public ReadCache(ISource<Key, Value> src) : base(src)
        {
            WithCache(new Dictionary<Key, Value>());
        }

        public ReadCache<Key, Value> WithCache(Dictionary<Key, Value> cache)
        {
            // EthereumJ does something with synchronising here
            this.cache = cache;
            return this;
        }

        public ReadCache<Key, Value> withMaxCapacity(int maxCapacity)
        {
            throw new NotImplementedException(); // Shouldn't need surely.
        }

        private bool isChecked = false;

        private void CheckByteArrKey(Key key)
        {
            return; // this is SO DUMB 

            //if (isChecked) return;

            //if (key is byte[]) {
            //    if (!byteKeyMap)
            //    {
            //        throw new Exception("Wrong map/set for byte[] key");
            //    }
            //}
            //isChecked = true;
        }

        public override void Put(Key key, Value val)
        {
            CheckByteArrKey(key); // IS this necessary? Dumb? Just make it for always bytes?
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

        public override Value Get(Key key)
        {
            CheckByteArrKey(key);
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

        public override void Delete(Key key)
        {
            CheckByteArrKey(key);
            Value value = cache[key];
            cache.Remove(key);
            CacheRemoved(key, value);
            GetSource().Delete(key);
        }

        protected override bool FlushImpl()
        {
            return false;
        }

        public override ICollection<Key> GetModified() {
            return new List<Key>();
        }

        public override bool HasModified()
        {
            return false;
        }

        public override Entry<Value> GetCached(Key key) {
            Value value = default(Value);
            if (cache.ContainsKey(key))
                value = cache[key];
            return value == null ? null : new SimpleEntry<Value>(value);
        }
        
    }
}
