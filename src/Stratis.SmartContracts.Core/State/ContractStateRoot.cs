using Stratis.Patricia;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Really magic class. Has an underlying KV store injected into the constructor. Everything is stored in this KV store 
    /// but in a series of tries that allow us to retrieve a 32-byte root that represents the current state. 
    /// This 32-byte root can be used to rollback state as well.
    /// 
    /// What's happening here:
    /// -A basic underlying byte[]/byte[] K/V store is injected in the constructor. In live daemon this will be a DbreezeByteStore.
    /// -A complex caching structure is built up. Any changes through the IContractStateRepository API are pushed into the cache 
    /// (e.g. SetCode, SetStorageValue).
    /// -Commit() will push all of the data inside the cache into the underlying K/V store, via a patricia trie.
    /// -The current state can now be represented by the 'root' retrieved from GetRoot()
    /// -Now if we ever need to load the current state, we can do GetSnapShotTo(root)
    /// 
    /// The ideal pattern:
    /// -Get a new repository object via StartTracking()
    /// -Make all changes to this repository
    /// -If all successful, do commit() and changes will be pushed to the root.
    /// -If unsuccessful, just rollback() the particular changes that didn't work.
    /// 
    /// </summary>
    public class ContractStateRoot : ContractState, IContractStateRoot
    {
        private class StorageCache : ReadWriteCache<byte[]>
        {
            public IPatriciaTrie trie;

            public StorageCache(IPatriciaTrie trie) : base(new SourceCodec<byte[], byte[], byte[], byte[]>(trie, new Serializers.NoSerializer<byte[]>(), new Serializers.NoSerializer<byte[]>()), WriteCache<byte[]>.CacheType.SIMPLE)
            {
                this.trie = trie;
            }
        }

        private class MultiStorageCache : MultiCache<ICachedSource<byte[], byte[]>>
        {
            ContractStateRoot parentRepo;

            public MultiStorageCache(ContractStateRoot parentRepo) : base(null)
            {
                this.parentRepo = parentRepo;
            }

            protected override ICachedSource<byte[], byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
            {
                AccountState accountState = this.parentRepo.accountStateCache.Get(key);
                IPatriciaTrie storageTrie = this.parentRepo.CreateTrie(this.parentRepo.trieCache, accountState?.StateRoot);
                return new StorageCache(storageTrie);
            }

            protected override bool FlushChild(byte[] key, ICachedSource<byte[], byte[]> childCache)
            {
                if (base.FlushChild(key, childCache))
                {
                    if (childCache != null)
                    {
                        StorageCache storageChildCache = (StorageCache)childCache; // praying to the lord this works
                        AccountState storageOwnerAcct = this.parentRepo.accountStateCache.Get(key);
                        // need to update account storage root
                        storageChildCache.trie.Flush();
                        byte[] rootHash = storageChildCache.trie.GetRootHash();
                        storageOwnerAcct.StateRoot = rootHash;
                        this.parentRepo.accountStateCache.Put(key, storageOwnerAcct);
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

        public byte[] Root
        {
            get
            {
                return GetRoot();
            }
        }

        private ISource<byte[], byte[]> stateDS;

        private ICachedSource<byte[], byte[]> trieCache;
        private IPatriciaTrie stateTrie;

        public ContractStateRoot() { }

        public ContractStateRoot(NoDeleteContractStateSource stateDS) : this(stateDS, null) { }

        public ContractStateRoot(ISource<byte[], byte[]> stateDS) : this(stateDS, null) { }

        public ContractStateRoot(ISource<byte[], byte[]> stateDS, byte[] stateRoot)
        {
            this.stateDS = stateDS;
            this.trieCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            this.stateTrie = new PatriciaTrie(stateRoot, this.trieCache);

            SourceCodec<byte[], AccountState, byte[], byte[]> accountStateCodec = new SourceCodec<byte[], AccountState, byte[], byte[]>(this.stateTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);
            ReadWriteCache<AccountState> accountStateCache = new ReadWriteCache<AccountState>(accountStateCodec, WriteCache<AccountState>.CacheType.SIMPLE);

            MultiCache<ICachedSource<byte[], byte[]>> storageCache = new MultiStorageCache(this);
            ISource<byte[], byte[]> codeCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            ISource<byte[], byte[]> unspentCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.SIMPLE);
            SourceCodec<byte[], ContractUnspentOutput, byte[], byte[]> unspentCacheCodec = new SourceCodec<byte[], ContractUnspentOutput, byte[], byte[]>(unspentCache, new Serializers.NoSerializer<byte[]>(), Serializers.VinSerializer);
            this.Init(accountStateCache, codeCache, storageCache, unspentCacheCodec);
        }

        public override void Commit()
        {
            base.Commit();
            this.stateTrie.Flush();
            this.trieCache.Flush();
        }

        private byte[] GetRoot()
        {
            this.storageCache.Flush();
            this.accountStateCache.Flush();

            return this.stateTrie.GetRootHash();
        }

        public override void Flush()
        {
            this.Commit();
        }

        public override IContractStateRoot GetSnapshotTo(byte[] stateRoot)
        {
            return new ContractStateRoot(this.stateDS, stateRoot);
        }

        public void SyncToRoot(byte[] root)
        {
            this.stateTrie.SetRootHash(root);
        }

        protected PatriciaTrie CreateTrie(ICachedSource<byte[], byte[]> trieCache, byte[] root)
        {
            return new PatriciaTrie(root, trieCache);
        }
    }
}
