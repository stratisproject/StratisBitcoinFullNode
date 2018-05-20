using System.Text;
using Stratis.Patricia;
using Xunit;
using MemoryDictionarySource = Stratis.Patricia.MemoryDictionarySource;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class TestTrie
    {
        private static readonly byte[] empty = new byte[0];
        private static readonly byte[] dog = Encoding.UTF8.GetBytes("dog");
        private static readonly byte[] dodecahedron = Encoding.UTF8.GetBytes("dodecahedron");
        private static readonly byte[] cat = Encoding.UTF8.GetBytes("cat");
        private static readonly byte[] fish = Encoding.UTF8.GetBytes("fish");
        private static readonly byte[] bird = Encoding.UTF8.GetBytes("bird");

        [Fact]
        public void TestTrieDeterminism()
        {
            // No matter the order that things are put in, if the contents are the same then root hash is the same
            var trie1 = new PatriciaTrie();
            var trie2 = new PatriciaTrie();

            trie1.Put(dog, cat);
            trie1.Put(fish, bird);

            trie2.Put(fish, bird);
            trie2.Put(dog, cat);

            Assert.Equal(trie1.GetRootHash(), trie2.GetRootHash());

            trie1.Put(dog, bird);
            trie1.Put(dog, fish);
            trie1.Put(dodecahedron, dog);
            trie1.Put(dodecahedron, cat);
            trie1.Put(fish, bird);
            trie1.Put(fish, cat);

            trie2.Put(dog, fish);
            trie2.Put(fish, cat);
            trie2.Put(dodecahedron, cat);

            Assert.Equal(trie1.GetRootHash(), trie2.GetRootHash());
        }

        [Fact]
        public void TestTrieGetPut()
        {
            // We can retrieve the values we put in
            var trie = new PatriciaTrie();

            trie.Put(dog, cat);
            trie.Put(fish, bird);

            Assert.Equal(cat, trie.Get(dog));
            Assert.Equal(bird, trie.Get(fish));

            trie.Put(dog, fish);
            trie.Put(dog, bird);

            Assert.Equal(bird, trie.Get(dog));
        }

        [Fact]
        public void TestTrieFlush()
        {
            var memDb = new MemoryDictionarySource();
            var trie = new PatriciaTrie(memDb);

            trie.Put(dog, cat);
            trie.Put(fish, bird);
            Assert.Equal(cat, trie.Get(dog));

            Assert.Empty(memDb.Db.Keys);
            trie.Flush();
            Assert.NotEmpty(memDb.Db.Keys); // This should be more specific in future. How many nodes are we expecting?
        }

        [Fact]
        public void TestTrieGetAfterFlush()
        {
            var memDb = new MemoryDictionarySource();
            var trie = new PatriciaTrie(memDb);

            trie.Put(dog, cat);
            Assert.Equal(cat, trie.Get(dog));
            trie.Flush();
            Assert.Equal(cat, trie.Get(dog));
        }

        [Fact]
        public void TestDelete()
        {
            var memDb = new MemoryDictionarySource();
            var trie = new PatriciaTrie(memDb);

            trie.Put(dog, cat);

            byte[] dogCatOnlyHash = trie.GetRootHash();

            trie.Put(fish, bird);
            trie.Delete(fish);

            Assert.Equal(dogCatOnlyHash, trie.GetRootHash());

            trie.Put(fish, bird);
            trie.Put(fish, empty);

            Assert.Equal(dogCatOnlyHash, trie.GetRootHash());
        }

        [Fact]
        public void TestTrieLoad()
        {
            var memDb = new MemoryDictionarySource();
            var trie = new PatriciaTrie(memDb);

            trie.Put(dog, cat);
            trie.Put(fish, bird);
            trie.Put(dodecahedron, fish);
            trie.Flush();
            byte[] savedHash = trie.GetRootHash();

            var trie2 = new PatriciaTrie(memDb);
            trie2.SetRootHash(savedHash);

            Assert.Equal(cat, trie.Get(dog));
            Assert.Equal(cat, trie2.Get(dog));
            Assert.Equal(bird, trie2.Get(fish));
            Assert.Equal(fish, trie2.Get(dodecahedron));
        }
    }
}
