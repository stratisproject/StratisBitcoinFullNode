using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public class StorageCaches : ISource<byte[], StorageCache>
    {
        // TODO: Any threading related stuff
        protected Dictionary<byte[], StorageCache> cache = new Dictionary<byte[],StorageCache>(new ByteArrayComparer());

        private StateRepositoryRoot repo;

        public StorageCaches(StateRepositoryRoot repo)
        {
            this.repo = repo;
        }

        public StorageCache Get(byte[] key)
        {
            if (this.cache.ContainsKey(key))
                return this.cache[key];

            AccountState accountState = this.repo.accountStateCache.Get(key);
            IPatriciaTrie storageTrie = this.repo.GetTrieWithSameCache(accountState?.StateRoot);
            var newCache = new StorageCache(storageTrie);
            this.cache[key] = newCache;
            return newCache;
        }

        public void Put(byte[] key, StorageCache val)
        {
            throw new NotImplementedException();
        }

        public void Delete(byte[] key)
        {
            throw new NotImplementedException();
        }

        public bool Flush()
        {
            bool ret = false;
            foreach (KeyValuePair<byte[], StorageCache> kvp in this.cache)
            {
                StorageCache childCache = kvp.Value;
                ret |= childCache.Flush();
                AccountState storageOwnerAcct = this.repo.accountStateCache.Get(kvp.Key);
                // need to update account storage root
                childCache.trie.Flush();
                byte[] rootHash = childCache.trie.GetRootHash();
                storageOwnerAcct.StateRoot = rootHash;
                this.repo.accountStateCache.Put(kvp.Key, storageOwnerAcct);
            }
            return ret;
        }
    }

    public class CachedStorageCaches : ISource<byte[], StorageCache>
    {
        public void Put(byte[] key, StorageCache val)
        {
            throw new NotImplementedException();
        }

        public StorageCache Get(byte[] key)
        {
            throw new NotImplementedException();
        }

        public void Delete(byte[] key)
        {
            throw new NotImplementedException();
        }

        public bool Flush()
        {
            throw new NotImplementedException();
        }
    }
}
