using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ.
    /// 
    /// A cache of many storage caches.
    /// </summary>
    public class MultiStorageCache : MultiCacheBase<ICachedSource<byte[], byte[]>>
    {
        ContractStateRoot parentRepo;

        public MultiStorageCache(ContractStateRoot parentRepo) : base(null)
        {
            this.parentRepo = parentRepo;
        }

        protected override ICachedSource<byte[], byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
        {
            AccountState accountState = this.parentRepo.accountStateCache.Get(key);
            IPatriciaTrie storageTrie = this.parentRepo.GetTrieWithSameCache(accountState?.StateRoot);
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
}
