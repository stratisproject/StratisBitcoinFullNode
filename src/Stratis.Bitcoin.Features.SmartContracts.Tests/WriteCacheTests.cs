using System;
using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts.Hashing;
using Stratis.SmartContracts.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class WriteCacheTest
    {

        private byte[] IntToKey(int i)
        {
            return HashHelper.Keccak256(BitConverter.GetBytes(i));
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

            //// Everything is flushed
            writeCache.Flush();
            Assert.Null(writeCache.GetCached(IntToKey(0)));
            Assert.Null(writeCache.GetCached(IntToKey(9_999)));
            Assert.Equal(ToHexString(IntToValue(9_999)), ToHexString(writeCache.Get(IntToKey(9_999))));
            Assert.Equal(ToHexString(IntToValue(0)), ToHexString(writeCache.Get(IntToKey(0))));
            //// Get not caches, only write cache
            Assert.Null(writeCache.GetCached(IntToKey(0)));

            //// Deleting key that is currently in cache
            //writeCache.put(intToKey(0), intToValue(12345));
            //assertEquals(str(intToValue(12345)), str(writeCache.getCached(intToKey(0)).value()));
            //writeCache.delete(intToKey(0));
            //assertTrue(null == writeCache.getCached(intToKey(0)) || null == writeCache.getCached(intToKey(0)).value());
            //assertEquals(str(intToValue(0)), str(src.get(intToKey(0))));
            //writeCache.flush();
            //assertNull(src.get(intToKey(0)));

            //// Deleting key that is not currently in cache
            //assertTrue(null == writeCache.getCached(intToKey(1)) || null == writeCache.getCached(intToKey(1)).value());
            //assertEquals(str(intToValue(1)), str(src.get(intToKey(1))));
            //writeCache.delete(intToKey(1));
            //assertTrue(null == writeCache.getCached(intToKey(1)) || null == writeCache.getCached(intToKey(1)).value());
            //assertEquals(str(intToValue(1)), str(src.get(intToKey(1))));
            //writeCache.flush();
            //assertNull(src.get(intToKey(1)));
        }

        //@Test
        //public void testCounting()
        //{
        //    Source<byte[], byte[]> parentSrc = new HashMapDB<>();
        //    Source<byte[], byte[]> src = new CountingBytesSource(parentSrc);
        //    WriteCache<byte[], byte[]> writeCache = new WriteCache.BytesKey<>(src, WriteCache.CacheType.COUNTING);
        //    for (int i = 0; i < 100; ++i)
        //    {
        //        for (int j = 0; j <= i; ++j)
        //        {
        //            writeCache.put(intToKey(i), intToValue(i));
        //        }
        //    }
        //    // Everything is cached
        //    assertEquals(str(intToValue(0)), str(writeCache.getCached(intToKey(0)).value()));
        //    assertEquals(str(intToValue(99)), str(writeCache.getCached(intToKey(99)).value()));

        //    // Everything is flushed
        //    writeCache.flush();
        //    assertNull(writeCache.getCached(intToKey(0)));
        //    assertNull(writeCache.getCached(intToKey(99)));
        //    assertEquals(str(intToValue(99)), str(writeCache.get(intToKey(99))));
        //    assertEquals(str(intToValue(0)), str(writeCache.get(intToKey(0))));

        //    // Deleting key which has 1 ref
        //    writeCache.delete(intToKey(0));

        //    // for counting cache we return the cached value even if
        //    // it was deleted (once or several times) as we don't know
        //    // how many 'instances' are left behind

        //    // but when we delete entry which is not in the cache we don't
        //    // want to spend unnecessary time for getting the value from
        //    // underlying storage, so getCached may return null.
        //    // get() should work as expected
        //    //        assertEquals(str(intToValue(0)), str(writeCache.getCached(intToKey(0))));

        //    assertEquals(str(intToValue(0)), str(src.get(intToKey(0))));
        //    writeCache.flush();
        //    assertNull(writeCache.getCached(intToKey(0)));
        //    assertNull(src.get(intToKey(0)));

        //    // Deleting key which has 2 refs
        //    writeCache.delete(intToKey(1));
        //    writeCache.flush();
        //    assertEquals(str(intToValue(1)), str(writeCache.get(intToKey(1))));
        //    writeCache.delete(intToKey(1));
        //    writeCache.flush();
        //    assertNull(writeCache.get(intToKey(1)));
        //}

        //@Test
        //public void testWithSizeEstimator()
        //{
        //    Source<byte[], byte[]> src = new HashMapDB<>();
        //    WriteCache<byte[], byte[]> writeCache = new WriteCache.BytesKey<>(src, WriteCache.CacheType.SIMPLE);
        //    writeCache.withSizeEstimators(MemSizeEstimator.ByteArrayEstimator, MemSizeEstimator.ByteArrayEstimator);
        //    assertEquals(0, writeCache.estimateCacheSize());

        //    writeCache.put(intToKey(0), intToValue(0));
        //    assertNotEquals(0, writeCache.estimateCacheSize());
        //    long oneObjSize = writeCache.estimateCacheSize();

        //    for (int i = 0; i < 100; ++i)
        //    {
        //        for (int j = 0; j <= i; ++j)
        //        {
        //            writeCache.put(intToKey(i), intToValue(i));
        //        }
        //    }
        //    assertEquals(oneObjSize * 100, writeCache.estimateCacheSize());

        //    writeCache.flush();
        //    assertEquals(0, writeCache.estimateCacheSize());
        //}
    }
}
