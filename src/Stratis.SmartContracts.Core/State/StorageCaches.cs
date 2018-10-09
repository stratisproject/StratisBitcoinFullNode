using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public class StorageCaches : ISource<byte[], ISource<byte[],byte[]>>
    {
        // TODO: Any threading related stuff
        protected Dictionary<byte[], StorageCache> cache = new Dictionary<byte[],StorageCache>(new ByteArrayComparer());

        private StateRepositoryRoot repo;

        public StorageCaches(StateRepositoryRoot repo)
        {
            this.repo = repo;
        }

        public ISource<byte[], byte[]> Get(byte[] key)
        {
            if (this.cache.ContainsKey(key))
                return this.cache[key];

            AccountState accountState = this.repo.accountStateCache.Get(key);
            IPatriciaTrie storageTrie = this.repo.GetTrieWithSameCache(accountState?.StateRoot);
            var newCache = new StorageCache(storageTrie);
            this.cache[key] = newCache;
            return newCache;
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
                if (storageOwnerAcct != null)
                {
                    childCache.trie.Flush();
                    byte[] rootHash = childCache.trie.GetRootHash();
                    storageOwnerAcct.StateRoot = rootHash;
                    this.repo.accountStateCache.Put(kvp.Key, storageOwnerAcct);
                }
            }

            this.cache.Clear();

            return ret;
        }

        public void Put(byte[] key, ISource<byte[], byte[]> val)
        {
            throw new NotImplementedException();
        }

        public void Delete(byte[] key)
        {
            throw new NotImplementedException();
        }
    }

    public class CachedStorageCaches : ISource<byte[], ISource<byte[], byte[]>>
    {
        private ISource<byte[], ISource<byte[], byte[]>> previous;

        private Dictionary<byte[], ISource<byte[], byte[]>> cache = new Dictionary<byte[], ISource<byte[], byte[]>>(new ByteArrayComparer());

        public CachedStorageCaches(ISource<byte[], ISource<byte[], byte[]>> previous)
        {
            this.previous = previous;
        }

        public ISource<byte[],byte[]> Get(byte[] key)
        {
            if (this.cache.ContainsKey(key))
                return this.cache[key];

            ISource<byte[], byte[]> newStorage = this.previous.Get(key);
            this.cache[key] = new WriteCache<byte[]>(newStorage, WriteCache<byte[]>.CacheType.SIMPLE);
            return this.cache[key];
        }

        public bool Flush()
        {
            bool ret = false;
            foreach(KeyValuePair<byte[], ISource<byte[], byte[]>> source in this.cache)
            {
                ret |= source.Value.Flush();
            }

            this.cache.Clear();

            return ret;
        }

        public void Put(byte[] key, ISource<byte[], byte[]> val)
        {
            throw new NotImplementedException();
        }

        public void Delete(byte[] key)
        {
            throw new NotImplementedException();
        }
    }
}
