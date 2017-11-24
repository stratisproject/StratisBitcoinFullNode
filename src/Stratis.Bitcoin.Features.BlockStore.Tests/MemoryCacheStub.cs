using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    internal class CacheEntryStub : ICacheEntry
    {
        public CacheEntryStub(object key, object value)
        {
            this.Key = key;
            this.Value = value;
        }

        public DateTimeOffset? AbsoluteExpiration
        {
            get; set;
        }

        public TimeSpan? AbsoluteExpirationRelativeToNow
        {
            get; set;
        }

        public IList<IChangeToken> ExpirationTokens
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public long? Size
        {
            get; set;
        }

        public object Key { get; }

        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks
        {
            get;
        }

        public CacheItemPriority Priority
        {
            get; set;
        }

        public TimeSpan? SlidingExpiration
        {
            get; set;
        }

        public object Value { get; set; }

        public void Dispose()
        {
        }
    }

    internal class MemoryCacheStub : IMemoryCache
    {
        public IDictionary<object, object> internalDict;
        private object lastCreateCalled;

        public MemoryCacheStub() : this(new Dictionary<object, object>())
        {
        }

        public object GetLastCreateCalled()
        {
            return this.lastCreateCalled;
        }

        public MemoryCacheStub(IDictionary<object, object> dict)
        {
            this.lastCreateCalled = null;
            this.internalDict = dict;
        }

        public ICacheEntry CreateEntry(object key)
        {
            this.lastCreateCalled = key;
            this.internalDict.Add(key, null);
            return new CacheEntryStub(key, null);
        }

        public void Dispose()
        {
        }

        public void Remove(object key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(object key, out object value)
        {
            return this.internalDict.TryGetValue(key, out value);
        }
    }
}
