using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    public interface IAsyncDictionary<TKey, TValue>
    {
        Task<int> Count { get; }
        Task<Collection<TKey>> Keys { get; }
        Task<Collection<TValue>> Values { get; }

        Task Add(TKey key, TValue value);
        Task Clear();
        Task<bool> ContainsKey(TKey key);
        Task<bool> Remove(TKey key);
        Task<TValue> TryGetValue(TKey key);
    }

    /// <summary>
    /// An attempt at making an async dictionary
    /// </summary>
    public class AsyncDictionary<TKey, TValue> : IAsyncDictionary<TKey, TValue>
    {
        private readonly IDictionary<TKey, TValue> dictionary;
        private readonly IAsyncLock asyncLock;

        public AsyncDictionary() : this(new AsyncLock(), new Dictionary<TKey, TValue>())
        {
        }

        public AsyncDictionary(IAsyncLock asyncLock) : this(asyncLock, new Dictionary<TKey, TValue>())
        {
        }

        internal AsyncDictionary(IAsyncLock asyncLock, IDictionary<TKey, TValue> dict)
        {
            Guard.NotNull(asyncLock, nameof(asyncLock));
            Guard.NotNull(dict, nameof(dict));

            this.asyncLock = asyncLock;
            this.dictionary = dict;
        }

        public Task Add(TKey key, TValue value)
        {
            return this.asyncLock.WriteAsync(() => this.dictionary.Add(key, value));
        }

        public Task Clear()
        {
            return this.asyncLock.WriteAsync(() => this.dictionary.Clear());
        }

        public Task<int> Count
        {
            get
            {
                return this.asyncLock.ReadAsync(() => this.dictionary.Count);
            }
        }

        public Task<bool> ContainsKey(TKey key)
        {
            return this.asyncLock.ReadAsync(() => this.dictionary.ContainsKey(key));
        }

        public Task<bool> Remove(TKey key)
        {
            return this.asyncLock.WriteAsync(() => this.dictionary.Remove(key));
        }

        public Task<TValue> TryGetValue(TKey key)
        {
            return this.asyncLock.ReadAsync(() =>
            {
                TValue outval;
                this.dictionary.TryGetValue(key, out outval);
                return outval;
            });
        }

        public Task<Collection<TKey>> Keys
        {
            get
            {
                return this.asyncLock.ReadAsync(() => new Collection<TKey>(this.dictionary.Keys.ToList()));
            }
        }

        public Task<Collection<TValue>> Values
        {
            get
            {
                return this.asyncLock.ReadAsync(() => new Collection<TValue>(this.dictionary.Values.ToList()));
            }
        }

    }
}
