using Moq;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncDictionaryTest
    {
        private Mock<IAsyncLock> asyncLock;
        private AsyncDictionary<string, decimal> dictionary;
        private IDictionary<string, decimal> underlyingDict;

        public AsyncDictionaryTest()
        {
            this.asyncLock = new Mock<IAsyncLock>();
            this.underlyingDict = new Dictionary<string, decimal>();
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);
        }

        [Fact]
        public void AddWritesValuesToDictionary()
        {
            this.asyncLock.Setup(a => a.WriteAsync(It.IsAny<Action>()))
                .Callback<Action>((a) => { a(); });

            this.dictionary.Add("txamt", 15.00m);

            Assert.Equal(1, this.underlyingDict.Keys.Count);
            Assert.Equal(15.00m, this.underlyingDict["txamt"]);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void ClearsValuesFromDictionary()
        {
            this.underlyingDict.Add("key", 15m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup(a => a.WriteAsync(It.IsAny<Action>()))
               .Callback<Action>((a) => { a(); });

            this.dictionary.Clear();

            Assert.Equal(0, this.underlyingDict.Keys.Count);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void CountReturnsCountFRomDictionar()
        {
            this.underlyingDict.Add("key", 15m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<int>>((a) => a.ReadAsync(It.IsAny<Func<int>>()))
               .Returns<Func<int>>((a) =>
               {
                   return Task.FromResult(a());
               });

            var task = this.dictionary.Count;
            task.Wait();

            Assert.Equal(1, task.Result);
            this.asyncLock.VerifyAll();
        }


        [Fact]
        public void ContainsKeyReturnsTrueIfDictionaryHasKey()
        {
            this.underlyingDict.Add("key", 15m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<bool>>((a) => a.ReadAsync(It.IsAny<Func<bool>>()))
                 .Returns<Func<bool>>((a) =>
                 {
                     return Task.FromResult(a());
                 });

            var task = this.dictionary.ContainsKey("key");
            task.Wait();

            Assert.True(task.Result);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void ContainsKeyReturnsFalseIfDictionaryDoesNotHasKey()
        {
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<bool>>((a) => a.ReadAsync(It.IsAny<Func<bool>>()))
                .Returns<Func<bool>>((a) =>
                {
                    return Task.FromResult(a());
                });

            var task = this.dictionary.ContainsKey("key");
            task.Wait();

            Assert.False(task.Result);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void TryGetValueReturnsValueIfDictionaryHasKey()
        {
            this.underlyingDict.Add("key", 15m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<decimal>>((a) => a.ReadAsync(It.IsAny<Func<decimal>>()))
               .Returns<Func<decimal>>((a) =>
               {
                   return Task.FromResult(a());
               });

            var task = this.dictionary.TryGetValue("key");
            task.Wait();

            Assert.Equal(15m, task.Result);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void TryGetValueReturnsNullIfDictionaryDoesNotHasKey()
        {
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<decimal>>((a) => a.ReadAsync(It.IsAny<Func<decimal>>()))
                .Returns<Func<decimal>>((a) =>
                {
                    return Task.FromResult(a());
                });

            var task = this.dictionary.TryGetValue("key");
            task.Wait();

            Assert.Equal(0, task.Result);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void RemoveDeletesKeyFromDictionary()
        {
            this.underlyingDict.Add("key", 15m);
            this.underlyingDict.Add("key2", 30m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);

            this.asyncLock.Setup<Task<bool>>((a) => a.WriteAsync(It.IsAny<Func<bool>>()))
               .Returns<Func<bool>>((a) =>
               {
                   return Task.FromResult(a());
               });

            var task = this.dictionary.Remove("key");
            task.Wait();

            Assert.True(task.Result);
            Assert.Equal(1, this.underlyingDict.Count);
            Assert.Equal("key2", this.underlyingDict.Keys.First());
            this.asyncLock.VerifyAll();
        }


        [Fact]
        public void KeysReturnsKeysFromDictionary()
        {
            this.underlyingDict.Add("key", 15m);
            this.underlyingDict.Add("key2", 30m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);
            this.asyncLock.Setup<Task<Collection<string>>>((a) => a.ReadAsync(It.IsAny<Func<Collection<string>>>()))
             .Returns<Func<Collection<string>>>((a) =>
             {
                 return Task.FromResult(a());
             });

            var task = this.dictionary.Keys;
            task.Wait();

            Assert.Equal(2, task.Result.Count);
            Assert.Equal("key", task.Result[0]);
            Assert.Equal("key2", task.Result[1]);
            this.asyncLock.VerifyAll();
        }

        [Fact]
        public void ValuesReturnsValuesFromDictionary()
        {
            this.underlyingDict.Add("key", 15m);
            this.underlyingDict.Add("key2", 30m);
            this.dictionary = new AsyncDictionary<string, decimal>(this.asyncLock.Object, this.underlyingDict);
            this.asyncLock.Setup<Task<Collection<decimal>>>((a) => a.ReadAsync(It.IsAny<Func<Collection<decimal>>>()))
             .Returns<Func<Collection<decimal>>>((a) =>
             {
                 return Task.FromResult(a());
             });

            var task = this.dictionary.Values;
            task.Wait();

            Assert.Equal(2, task.Result.Count);
            Assert.Equal(15m, task.Result[0]);
            Assert.Equal(30m, task.Result[1]);
            this.asyncLock.VerifyAll();
        }
    }
}
