#if !NOSOCKET
using System;
using System.Collections.Concurrent;

namespace NBitcoin
{
    public class BoundedDictionary<TKey, TValue>
    {
        readonly uint maxItems;
        ConcurrentDictionary<TKey, TValue> _Dictionary = new ConcurrentDictionary<TKey, TValue>();
        ConcurrentQueue<TKey> _Queue = new ConcurrentQueue<TKey>();

        public BoundedDictionary(uint maxItems)
        {
            this.maxItems = maxItems;
        }

        public int Count
        {
            get
            {
                return _Dictionary.Count;
            }
        }

        public TValue AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> update)
        {
            bool wasPresent = false;
            TValue val = _Dictionary.AddOrUpdate(key, value, (k, v) =>
            {
                wasPresent = true;
                return update(k, v);
            });
            if(!wasPresent)
            {
                _Queue.Enqueue(key);
                Clean();
            }
            return val;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            var added = _Dictionary.TryAdd(key, value);
            if(added)
            {
                _Queue.Enqueue(key);
                Clean();
            }
            return added;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _Dictionary.TryGetValue(key, out value);
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            return _Dictionary.TryRemove(key, out value);
        }

        private void Clean()
        {
            while(_Queue.Count > maxItems)
            {
                TKey result;
                if(_Queue.TryDequeue(out result))
                {
                    TValue result2;
                    _Dictionary.TryRemove(result, out result2);
                }
            }
        }
    }
}
#endif