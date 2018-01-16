using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public abstract class AbstractCachedSource<Key, Value> :AbstractChainedSource<Key, Value, Key, Value>, ICachedSource<Key, Value>
    {

        private object aLock = new object();

        public interface Entry<V>
        {
            V Value();
        }

        public class SimpleEntry<V> : Entry<V> {
            private V val;

            public SimpleEntry(V val)
            {
                this.val = val;
            }
            public V Value()
            {
                return val;
            }
        }

        protected IMemSizeEstimator<Key> keySizeEstimator;
        protected IMemSizeEstimator<Value> valueSizeEstimator;
        private int size = 0;

        public AbstractCachedSource(ISource<Key, Value> source) : base(source)
        {
        }


        public abstract Entry<Value> GetCached(Key key);


        protected void CacheAdded(Key key, Value value)
        {
            lock (aLock)
            {
                if (keySizeEstimator != null)
                {
                    size += (int) keySizeEstimator.EstimateSize(key);
                }
                if (valueSizeEstimator != null)
                {
                    size += (int) valueSizeEstimator.EstimateSize(value);
                }
            }
        }

        protected void CacheRemoved(Key key, Value value)
        {
            lock (aLock)
            {
                if (keySizeEstimator != null)
                {
                    size -= (int) keySizeEstimator.EstimateSize(key);
                }
                if (valueSizeEstimator != null)
                {
                    size -= (int) valueSizeEstimator.EstimateSize(value);
                }
            }
        }

        protected void CacheCleared()
        {
            lock (aLock)
            {
                size = 0;
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
            return size;
        }

        public abstract ICollection<Key> GetModified();
        public abstract bool HasModified();
    }
}
