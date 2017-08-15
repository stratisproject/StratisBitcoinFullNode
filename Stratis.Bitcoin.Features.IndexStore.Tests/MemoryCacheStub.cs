using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
	internal class CacheEntryStub : ICacheEntry
	{
		private object key;
		private object value;

		public CacheEntryStub(object key, object value)
		{
			this.key = key;
			this.value = value;
		}

		public DateTimeOffset? AbsoluteExpiration
		{
			get; set;
		}

		public TimeSpan? AbsoluteExpirationRelativeToNow
		{
			get;set;
		}

		public IList<IChangeToken> ExpirationTokens
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public object Key
		{
			get
			{
				return this.key;
			}
		}

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

		public object Value
		{
			get
			{
				return this.value;
			}

			set
			{
				this.value = value;
			}
		}

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
