using DBreeze;
using DBreeze.Utils;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Hashing;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    // TODOs:

    // -Handle object serialization. At the moment trie only retrieves bytes

    // -Experiment with using only one db for everything - if it's all hashed before it goes in the db
    //  then theoretically we only need 1 KV store

    public class Repository : IRepository
    {
        protected Repository parent;
        public ISource<byte[], AccountState> accountStateCache;
        protected ISource<byte[], byte[]> codeCache;
        protected MultiCache<ICachedSource<byte[], byte[]>> storageCache;

        protected Repository() { }

        public Repository(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                      MultiCache<ICachedSource<byte[], byte[]>> storageCache)
        {
            Init(accountStateCache, codeCache, storageCache);
        }

        protected void Init(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                    MultiCache<ICachedSource<byte[], byte[]>> storageCache)
        {
            this.accountStateCache = accountStateCache;
            this.codeCache = codeCache;
            this.storageCache = storageCache;
        }

        public AccountState CreateAccount(uint160 addr)
        {
            AccountState state = new AccountState();
            accountStateCache.Put(addr.ToBytes(), state);
            return state;
        }

        public bool IsExist(uint160 addr)
        {
            return GetAccountState(addr) != null;
        }

        public AccountState GetAccountState(uint160 addr)
        {
            return accountStateCache.Get(addr.ToBytes());
        }

        private AccountState GetOrCreateAccountState(uint160 addr)
        {
            AccountState ret = accountStateCache.Get(addr.ToBytes());
            if (ret == null)
            {
                ret = CreateAccount(addr);
            }
            return ret;
        }

        public void Delete(uint160 addr)
        {
            accountStateCache.Delete(addr.ToBytes());
            storageCache.Delete(addr.ToBytes());
        }

        public void SaveCode(uint160 addr, byte[] code)
        {
            byte[] codeHash = HashHelper.Keccak256(code);
            codeCache.Put(codeHash, code);
            AccountState accountState = GetOrCreateAccountState(addr);
            accountState.CodeHash = codeHash;
            accountStateCache.Put(addr.ToBytes(), accountState);
        }

        public byte[] GetCode(uint160 addr)
        {
            byte[] codeHash = GetCodeHash(addr);
            return codeCache.Get(codeHash);
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState != null ? accountState.CodeHash : new byte[0]; // TODO: REPLACE THIS BYTE0 with something
        }

        public void AddStorageRow(uint160 addr, byte[] key, byte[] value)
        {
            GetOrCreateAccountState(addr);
            ISource<byte[], byte[]> contractStorage = storageCache.Get(addr.ToBytes());
            contractStorage.Put(key, value); // TODO: Check if 0
        }

        public byte[] GetStorageValue(uint160 addr, byte[] key)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState == null ? null : storageCache.Get(addr.ToBytes()).Get(key);
        }

        public IRepository StartTracking()
        {
            ISource<byte[], AccountState> trackAccountStateCache = new WriteCache<AccountState>(accountStateCache,
                    WriteCache<AccountState>.CacheType.SIMPLE);
            ISource<byte[], byte[]> trackCodeCache = new WriteCache<byte[]>(codeCache, WriteCache< byte[]>.CacheType.SIMPLE);
            MultiCache<ICachedSource<byte[], byte[]>> trackStorageCache = new RealMultiCache(storageCache);
            Repository ret = new Repository(trackAccountStateCache, trackCodeCache, trackStorageCache);
            ret.parent = this;
            return ret;
        }

        public IRepository GetSnapshotTo(byte[] root)
        {
            return parent.GetSnapshotTo(root);
        }

        public void Commit()
        {
            Repository parentSync = parent == null ? this : parent;
            lock(parentSync) {
                storageCache.Flush();
                codeCache.Flush();
                accountStateCache.Flush();
            }
        }

        public void Rollback()
        {
            // nothing to do, will be GCed
        }

        public byte[] GetRoot()
        {
            throw new Exception("Not supported");
        }

        public HashSet<uint160> GetAccountsKeys()
        {
            throw new Exception("Not supported");
        }

        public void Flush()
        {
            throw new Exception("Not supported");
        }

        public void FlushNoReconnect()
        {
            throw new Exception("Not supported");
        }

        public void SyncToRoot(byte[] root)
        {
            throw new Exception("Not supported");
        }

        public bool IsClosed()
        {
            throw new Exception("Not supported");
        }

        public void Close()
        {
        }

        public void Reset()
        {
            throw new Exception("Not supported");
        }

        public int GetStorageSize(byte[] addr)
        {
            throw new Exception("Not supported");
        }

        //public HashSet<byte[]> GetStorageKeys(byte[] addr)
        //{
        //    throw new Exception("Not supported");
        //}

        //public Map<DataWord, DataWord> getStorage(byte[] addr, @Nullable Collection<DataWord> keys)
        //{
        //    throw new RuntimeException("Not supported");
        //}

        //private DBreezeEngine engine = null;
        //private AccountStateTrie accountStateTrie;
        //private Dictionary<uint160, PatriciaTrie> contractStorageTries;

        //private const string AccountStateTable = "AccountState";
        //private const string CodeTable = "Code";
        //private const string DbLocation = @"C:\temp";

        //public Repository(string dbLocation = null)
        //{
        //    this.engine = new DBreezeEngine(dbLocation ?? DbLocation);
        //    this.accountStateTrie = new AccountStateTrie(new PatriciaTrie(new DBreezeByteStore(this.engine, AccountStateTable)));
        //    this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        //}

        //protected Repository(DBreezeEngine engine, AccountStateTrie accountStateTrie)
        //{
        //    this.engine = engine;
        //    this.accountStateTrie = accountStateTrie;
        //    this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        //}

        //public IRepository StartTracking()
        //{
        //    var bytes = accountStateTrie.GetRootHash();
        //    return new Repository(this.engine, new AccountStateTrie(new PatriciaTrie(new DBreezeByteStore(this.engine, AccountStateTable), this.accountStateTrie.GetRootHash())));
        //}

        ///// <summary>
        ///// Empty database
        ///// </summary>
        //public void Refresh()
        //{
        //    using (var t = engine.GetTransaction())
        //    {
        //        // TODO: Once the keys are all stored in a single table, just wipe out that table
        //        // At the moment each contract has a separate table that is storing things.
        //        t.RemoveAllKeys(AccountStateTable, true);
        //        t.RemoveAllKeys(CodeTable, true);
        //    }
        //}

        //public void SetCode(uint160 address, byte[] code)
        //{
        //    AccountState accountState = GetAccountState(address);
        //    accountState.CodeHash = HashHelper.Keccak256(code);
        //    this.accountStateTrie.Put(address, accountState);
        //    using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
        //    {
        //        t.Insert<byte[], byte[]>(CodeTable, accountState.CodeHash, code);
        //        t.Commit();
        //    }
        //}

        //public byte[] GetCode(uint160 address)
        //{
        //    AccountState accountState = GetAccountState(address);
        //    using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
        //    {
        //        DBreeze.DataTypes.Row<byte[], byte[]> row = t.Select<byte[], byte[]>(CodeTable, accountState.CodeHash);

        //        if (row.Exists)
        //            return row.Value;

        //        return null;
        //    }
        //}

        //public AccountState CreateAccount(uint160 address)
        //{
        //    var accountState = new AccountState();
        //    this.accountStateTrie.Put(address, accountState);
        //    return accountState;
        //}

        //private AccountState GetOrCreateAccountState(uint160 address)
        //{
        //    AccountState accountState = GetAccountState(address);
        //    if (accountState != null)
        //        return accountState;

        //    return CreateAccount(address);
        //}

        //private AccountState GetAccountState(uint160 address)
        //{
        //    return this.accountStateTrie.Get(address);
        //}

        //public void SetObject<T>(uint160 address, object key, T toStore)
        //{
        //    // TODO: Can be optimised. We're getting account state twice.
        //    PatriciaTrie trie = GetContractStorageTrie(address);

        //    //var test = trie.GetRootHash();

        //    trie.Put((byte[])key, (byte[])(object)toStore);

        //    //accountState.StateRoot = trie.GetRootHash();
        //    //this.accountStateTrie.Put(address, accountState);
        //}

        //public T GetObject<T>(uint160 address, object key)
        //{
        //    return (T)(object) GetContractStorageTrie(address).Get((byte[])key);
        //}

        //private PatriciaTrie GetContractStorageTrie(uint160 address)
        //{
        //    if (this.contractStorageTries.ContainsKey(address))
        //        return this.contractStorageTries[address];

        //    AccountState accountState = GetAccountState(address);
        //    PatriciaTrie contractStorageTrie = new PatriciaTrie(new DBreezeByteStore(this.engine, address.ToString()), accountState.StateRoot);
        //    this.contractStorageTries.Add(address, contractStorageTrie);
        //    return contractStorageTrie;
        //}

        //public void Commit()
        //{
        //    foreach(KeyValuePair<uint160,PatriciaTrie> kvp in this.contractStorageTries)
        //    {
        //        kvp.Value.Flush();
        //        AccountState accountState = GetAccountState(kvp.Key);
        //        accountState.StateRoot = kvp.Value.GetRootHash();
        //        this.accountStateTrie.Put(kvp.Key, accountState);
        //    }
        //    this.accountStateTrie.Flush();
        //    this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        //}

        //public void Rollback()
        //{
        //    // Do nothing - let garbage collection take care of it.
        //}

        //public void LoadSnapshot(byte[] root)
        //{
        //    this.accountStateTrie.SetRoot(root);
        //    this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        //}

        //public byte[] GetRoot()
        //{
        //    return this.accountStateTrie.GetRootHash();
        //}

    }
}