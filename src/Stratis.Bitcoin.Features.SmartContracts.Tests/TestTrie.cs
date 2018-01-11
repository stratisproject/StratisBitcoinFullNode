using System;
using Xunit;
using Stratis.SmartContracts.Trie;
using System.Text;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class TestTrie
    {
        [Fact]
        public void TestTrieAsync()
        {
            var trie = new Trie();
            byte[] dog = Encoding.UTF8.GetBytes("dog");
            byte[] cat = Encoding.UTF8.GetBytes("cat");
            trie.Put(dog, cat);

            Assert.Equal(trie.Get(dog), cat);
            var test = trie.GetRootHash();
        }
    }
}
