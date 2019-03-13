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
    public class StateRepositoryRoot : StateRepository, IStateRepositoryRoot
    {
        public byte[] Root
        {
            get
            {
                return this.GetRoot();
            }
        }

        private readonly ISource<byte[], byte[]> stateDS;

        private readonly ICachedSource<byte[], byte[]> trieCache;
        private readonly IPatriciaTrie stateTrie;

        public StateRepositoryRoot() { }

        public StateRepositoryRoot(NoDeleteContractStateSource stateDS) : this(stateDS, null) { }

        public StateRepositoryRoot(ISource<byte[], byte[]> stateDS) : this(stateDS, null) { }

        public StateRepositoryRoot(ISource<byte[], byte[]> stateDS, byte[] stateRoot)
        {
            this.stateDS = stateDS;
            this.trieCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            this.stateTrie = new PatriciaTrie(stateRoot, this.trieCache);

            this.SetupAsNew();
        }

        /// <summary>
        /// Creates brand new caches, just like starting from scratch.
        /// Useful when creating a new StateRepositoryRoot or trying to sync to another known state and wanting to abandon all "in-progress" data.
        /// </summary>
        private void SetupAsNew()
        {
            SourceCodec<byte[], AccountState, byte[], byte[]> accountStateCodec = new SourceCodec<byte[], AccountState, byte[], byte[]>(this.stateTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);
            WriteCache<AccountState> accountStateCache = new WriteCache<AccountState>(accountStateCodec, WriteCache<AccountState>.CacheType.SIMPLE);

            var storageCaches = new RootStorageCaches(this);
            ISource<byte[], byte[]> codeCache = new WriteCache<byte[]>(this.stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            ISource<byte[], byte[]> unspentCache = new WriteCache<byte[]>(this.stateDS, WriteCache<byte[]>.CacheType.SIMPLE);
            SourceCodec<byte[], ContractUnspentOutput, byte[], byte[]> unspentCacheCodec = new SourceCodec<byte[], ContractUnspentOutput, byte[], byte[]>(unspentCache, new Serializers.NoSerializer<byte[]>(), Serializers.ContractOutputSerializer);
            this.Init(accountStateCache, codeCache, storageCaches, unspentCacheCodec);
        }

        public override void Commit()
        {
            base.Commit();
            this.stateTrie.Flush();
            this.trieCache.Flush();
        }

        private byte[] GetRoot()
        {
            this.storageCaches.Flush();
            this.accountStateCache.Flush();

            return this.stateTrie.GetRootHash();
        }

        public override void Flush()
        {
            this.Commit();
        }

        public override IStateRepositoryRoot GetSnapshotTo(byte[] stateRoot)
        {
            return new StateRepositoryRoot(this.stateDS, stateRoot);
        }

        public void SyncToRoot(byte[] root)
        {
            this.SetupAsNew();

            this.stateTrie.SetRootHash(root);
        }

        public IPatriciaTrie GetTrieWithSameCache(byte[] root)
        {
            return new PatriciaTrie(root, this.trieCache);
        }
    }
}
