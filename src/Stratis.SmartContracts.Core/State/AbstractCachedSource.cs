using System.Collections.Generic;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public abstract class AbstractCachedSource<Key, Value> : AbstractChainedSource<Key, Value, Key, Value>, ICachedSource<Key, Value>
    {
        private object aLock = new object();

        public interface IEntry<V>
        {
            V Value();
        }

        public class SimpleEntry<V> : IEntry<V>
        {

            private V val;

            public SimpleEntry(V val)
            {
                this.val = val;
            }

            public V Value()
            {
                return this.val;
            }
        }

        protected IMemSizeEstimator<Key> keySizeEstimator;
        protected IMemSizeEstimator<Value> valueSizeEstimator;
        private long size = 0;

        public AbstractCachedSource(ISource<Key, Value> source) : base(source)
        {
        }

        public abstract IEntry<Value> GetCached(Key key);


        protected void CacheAdded(Key key, Value value)
        {
            lock (this.aLock)
            {
                if (this.keySizeEstimator != null)
                {
                    this.size += this.keySizeEstimator.EstimateSize(key);
                }
                if (this.valueSizeEstimator != null)
                {
                    this.size += this.valueSizeEstimator.EstimateSize(value);
                }
            }
        }

        protected void CacheRemoved(Key key, Value value)
        {
            lock (this.aLock)
            {
                if (this.keySizeEstimator != null)
                {
                    this.size -= this.keySizeEstimator.EstimateSize(key);
                }
                if (this.valueSizeEstimator != null)
                {
                    this.size -= (int)this.valueSizeEstimator.EstimateSize(value);
                }
            }
        }

        protected void CacheCleared()
        {
            lock (this.aLock)
            {
                this.size = 0;
            }
        }

        public AbstractCachedSource<Key, Value> withSizeEstimators(IMemSizeEstimator<Key> keySizeEstimator, IMemSizeEstimator<Value> valueSizeEstimator)
        {
            this.keySizeEstimator = keySizeEstimator;
            this.valueSizeEstimator = valueSizeEstimator;
            return this;
        }

        public long EstimateCacheSize()
        {
            return this.size;
        }

        public abstract ICollection<Key> GetModified();
        public abstract bool HasModified();
    }
}
