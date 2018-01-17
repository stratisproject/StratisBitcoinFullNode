using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// Really magic class. Any time you commit(), you can get the root and use that to load the state at that particular time. 
    /// </summary>
    public class RepositoryRoot : Repository
    {
        private class StorageCache : ReadWriteCache<byte[]>
        {
            public ITrie<byte[]> trie;

            public StorageCache(ITrie<byte[]> trie) : base(new SourceCodec<byte[],byte[],byte[],byte[]>(trie, new Serializers.NoSerializer<byte[]>(), new Serializers.NoSerializer<byte[]>()), WriteCache<byte[]>.CacheType.SIMPLE)
            {
                this.trie = trie;
            }
        }

        private class MultiStorageCache : MultiCache<ICachedSource<byte[], byte[]>>
        {
            RepositoryRoot parentRepo;

            public MultiStorageCache(RepositoryRoot parentRepo) : base(null)
            {
                this.parentRepo = parentRepo;
            }

            protected override ICachedSource<byte[],byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
            {
                AccountState accountState = parentRepo.accountStateCache.Get(key);
                PatriciaTrie storageTrie = parentRepo.CreateTrie(parentRepo.trieCache, accountState == null ? null : accountState.StateRoot);
                return new StorageCache(storageTrie);
            }

            protected override bool FlushChild(byte[] key, ICachedSource<byte[],byte[]> childCache)
            {
                if (base.FlushChild(key, childCache))
                {
                    if (childCache != null)
                    {
                        StorageCache storageChildCache = (StorageCache)childCache; // praying to the lord this works
                        AccountState storageOwnerAcct = parentRepo.accountStateCache.Get(key);
                        // need to update account storage root
                        storageChildCache.trie.Flush();
                        byte[] rootHash = storageChildCache.trie.GetRootHash();
                        storageOwnerAcct.StateRoot = rootHash;
                        parentRepo.accountStateCache.Put(key, storageOwnerAcct);
                        return true;
                    }
                    else
                    {
                        // account was deleted
                        return true;
                    }
                }
                else
                {
                    // no storage changes
                    return false;
                }
            }
        }

        //private class MultiStorageCache : MultiCache<StorageCache>
        //{
        //    RepositoryRoot parentRepo;

        //    public MultiStorageCache(RepositoryRoot parentRepo) : base(null)
        //    {
        //        this.parentRepo = parentRepo;
        //    }

        //    protected override StorageCache Create(byte[] key, StorageCache srcCache)
        //    {
        //        AccountState accountState = parentRepo.accountStateCache.Get(key);
        //        PatriciaTrie storageTrie = parentRepo.CreateTrie(parentRepo.trieCache, accountState == null ? null : accountState.StateRoot);
        //        return new StorageCache(storageTrie);
        //    }

        //    public new bool FlushChild(byte[] key, StorageCache childCache)
        //    {
        //        if (base.FlushChild(key, childCache))
        //        {
        //            if (childCache != null)
        //            {
        //                AccountState storageOwnerAcct = parentRepo.accountStateCache.Get(key);
        //                // need to update account storage root
        //                childCache.trie.Flush();
        //                byte[] rootHash = childCache.trie.GetRootHash();
        //                storageOwnerAcct.StateRoot = rootHash;
        //                parentRepo.accountStateCache.Put(key, storageOwnerAcct);
        //                return true;
        //            }
        //            else
        //            {
        //                // account was deleted
        //                return true;
        //            }
        //        }
        //        else
        //        {
        //            // no storage changes
        //            return false;
        //        }
        //    }
        //}
        
        private ISource<byte[], byte[]> stateDS;
        private ICachedSource<byte[], byte[]> trieCache;
        private ITrie<byte[]> stateTrie;


        public RepositoryRoot(ISource<byte[],byte[]> stateDS) : this(stateDS, null)
        {

        }

        public RepositoryRoot(ISource<byte[],byte[]> stateDS, byte[] root)
        {
            this.stateDS = stateDS;

            trieCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            stateTrie = new PatriciaTrie(trieCache, root);
            SourceCodec<byte[], AccountState, byte[], byte[]> accountStateCodec = new SourceCodec<byte[], AccountState, byte[], byte[]>(stateTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);
            ReadWriteCache<AccountState> accountStateCache = new ReadWriteCache<AccountState>(accountStateCodec, WriteCache<AccountState>.CacheType.SIMPLE);

            MultiCache<ICachedSource<byte[], byte[]>> storageCache = new MultiStorageCache(this);
            ISource<byte[], byte[]> codeCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);

            Init(accountStateCache, codeCache, storageCache);
        }

        public void Commit()
        {
            base.Commit();
            stateTrie.Flush();
            trieCache.Flush();
        }

        public byte[] GetRoot()
        {
            storageCache.Flush();
            accountStateCache.Flush();

            return stateTrie.GetRootHash();
        }

        public void Flush()
        {
            Commit();
        }

        public Repository GetSnapshotTo(byte[] root)
        {
            return new RepositoryRoot(stateDS, root);
        }

        public void SyncToRoot(byte[] root)
        {
            stateTrie.SetRoot(root);
        }

        protected PatriciaTrie CreateTrie(ICachedSource<byte[], byte[]> trieCache, byte[] root)
        {
            return new PatriciaTrie(trieCache, root);
        }



    }
}
