using System;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.State;
using Xunit;
using MemoryDictionarySource = Stratis.Patricia.MemoryDictionarySource;

namespace Stratis.SmartContracts.Core.Tests
{
    public class WriteCacheTest
    {
        private byte[] IntToKey(int i)
        {
            return Stratis.SmartContracts.Core.Hashing.HashHelper.Keccak256(BitConverter.GetBytes(i));
        }

        private byte[] IntToValue(int i)
        {
            return BitConverter.GetBytes(i);
        }

        private string ToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        [Fact]
        public void TestSimple()
        {
            ISource<byte[], byte[]> src = new MemoryDictionarySource();
            WriteCache<byte[]> writeCache = new WriteCache<byte[]>(src, WriteCache<byte[]>.CacheType.SIMPLE);
            for (int i = 0; i < 10_000; ++i)
            {
                writeCache.Put(IntToKey(i), IntToValue(i));
            }
            // Everything is cached
            Assert.Equal(ToHexString(IntToValue(0)), ToHexString(writeCache.GetCached(IntToKey(0)).Value()));
            Assert.Equal(ToHexString(IntToValue(9_999)), ToHexString(writeCache.GetCached(IntToKey(9_999)).Value()));

            // Everything is flushed
            writeCache.Flush();
            Assert.Null(writeCache.GetCached(IntToKey(0)));
            Assert.Null(writeCache.GetCached(IntToKey(9_999)));
            Assert.Equal(ToHexString(IntToValue(9_999)), ToHexString(writeCache.Get(IntToKey(9_999))));
            Assert.Equal(ToHexString(IntToValue(0)), ToHexString(writeCache.Get(IntToKey(0))));
            // Get not caches, only write cache
            Assert.Null(writeCache.GetCached(IntToKey(0)));

            // Deleting key that is currently in cache
            writeCache.Put(IntToKey(0), IntToValue(12345));
            Assert.Equal(ToHexString(IntToValue(12345)), ToHexString(writeCache.GetCached(IntToKey(0)).Value()));
            writeCache.Delete(IntToKey(0));
            Assert.True(null == writeCache.GetCached(IntToKey(0)) || null == writeCache.GetCached(IntToKey(0)).Value());
            Assert.Equal(ToHexString(IntToValue(0)), ToHexString(src.Get(IntToKey(0))));
            writeCache.Flush();
            Assert.Null(src.Get(IntToKey(0)));

            // Deleting key that is not currently in cache
            Assert.True(null == writeCache.GetCached(IntToKey(1)) || null == writeCache.GetCached(IntToKey(1)).Value());
            Assert.Equal(ToHexString(IntToValue(1)), ToHexString(src.Get(IntToKey(1))));
            writeCache.Delete(IntToKey(1));
            Assert.True(null == writeCache.GetCached(IntToKey(1)) || null == writeCache.GetCached(IntToKey(1)).Value());
            Assert.Equal(ToHexString(IntToValue(1)), ToHexString(src.Get(IntToKey(1))));
            writeCache.Flush();
            Assert.Null(src.Get(IntToKey(1)));
        }
    }
}
