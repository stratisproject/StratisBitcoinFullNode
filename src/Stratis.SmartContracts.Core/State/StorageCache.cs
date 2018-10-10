using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// A wrapper for a specific account's storage.
    /// </summary>
    public class StorageCache : WriteCache<byte[]>
    {
        public IPatriciaTrie trie;

        public StorageCache(IPatriciaTrie trie) : base(trie, WriteCache<byte[]>.CacheType.SIMPLE)
        {
            this.trie = trie;
        }
    }
}
