using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// When reading values, will cache them for performance.
    /// </summary>
    public class ReadCache<Value> : AbstractCachedSource<byte[], Value>
    {
        private Dictionary<byte[], Value> cache;

        public ReadCache(ISource<byte[], Value> src) : base(src)
        {
            this.WithCache(new Dictionary<byte[], Value>(new ByteArrayComparer()));
        }

        public ReadCache<Value> WithCache(Dictionary<byte[], Value> cache)
        {
            this.cache = cache;
            return this;
        }

        public override void Put(byte[] key, Value val)
        {
            if (val == null)
            {
                this.Delete(key);
            }
            else
            {
                this.cache[key] = val;
                this.Source.Put(key, val);
            }
        }

        public override Value Get(byte[] key)
        {
            Value ret = default(Value);
            if (this.cache.ContainsKey(key))
                ret = this.cache[key];

            if (ret == null)
            {
                ret = this.Source.Get(key);
                if (ret != null)
                    this.cache[key] = ret;
            }
            return ret;
        }

        public override void Delete(byte[] key)
        {
            Value value = this.cache[key];
            this.cache.Remove(key);
            this.Source.Delete(key);
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

        public override IEntry<Value> GetCached(byte[] key) {
            Value value = default(Value);
            if (this.cache.ContainsKey(key))
                value = this.cache[key];
            return value == null ? null : new SimpleEntry<Value>(value);
        }
        
    }
}
