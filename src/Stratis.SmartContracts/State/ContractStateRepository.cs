using DBreeze;
using DBreeze.Utils;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Hashing;
using NBitcoin;
using Stratis.SmartContracts.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.State
{
    public class ContractStateRepository : IContractStateRepository
    {
        protected ContractStateRepository parent;
        public ISource<byte[], AccountState> accountStateCache;
        public ISource<byte[], StoredVin> vinCache; 
        protected ISource<byte[], byte[]> codeCache;
        protected MultiCache<ICachedSource<byte[], byte[]>> storageCache;
        protected List<TransferInfo> transfers;

        protected ContractStateRepository() { }

        public ContractStateRepository(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                      MultiCache<ICachedSource<byte[], byte[]>> storageCache, ISource<byte[], StoredVin> vinCache)
        {
            Init(accountStateCache, codeCache, storageCache, vinCache);
        }

        protected void Init(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                    MultiCache<ICachedSource<byte[], byte[]>> storageCache, ISource<byte[], StoredVin> vinCache)
        {
            this.accountStateCache = accountStateCache;
            this.codeCache = codeCache;
            this.storageCache = storageCache;
            this.vinCache = vinCache;
            this.transfers = new List<TransferInfo>();
        }

        public AccountState CreateAccount(uint160 addr)
        {
            AccountState state = new AccountState();
            this.accountStateCache.Put(addr.ToBytes(), state);
            return state;
        }

        public bool IsExist(uint160 addr)
        {
            return GetAccountState(addr) != null;
        }

        public AccountState GetAccountState(uint160 addr)
        {
            return this.accountStateCache.Get(addr.ToBytes());
        }

        private AccountState GetOrCreateAccountState(uint160 addr)
        {
            AccountState ret = this.accountStateCache.Get(addr.ToBytes());
            if (ret == null)
            {
                ret = CreateAccount(addr);
            }
            return ret;
        }

        public void Delete(uint160 addr)
        {
            this.accountStateCache.Delete(addr.ToBytes());
            this.storageCache.Delete(addr.ToBytes());
        }

        public void SetCode(uint160 addr, byte[] code)
        {
            byte[] codeHash = HashHelper.Keccak256(code);
            this.codeCache.Put(codeHash, code);
            AccountState accountState = GetOrCreateAccountState(addr);
            accountState.CodeHash = codeHash;
            this.accountStateCache.Put(addr.ToBytes(), accountState);
        }

        public byte[] GetCode(uint160 addr)
        {
            byte[] codeHash = GetCodeHash(addr);
            return this.codeCache.Get(codeHash);
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState != null ? accountState.CodeHash : new byte[0]; // TODO: REPLACE THIS BYTE0 with something
        }

        public void SetStorageValue(uint160 addr, byte[] key, byte[] value)
        {
            GetOrCreateAccountState(addr);
            ISource<byte[], byte[]> contractStorage = this.storageCache.Get(addr.ToBytes());
            contractStorage.Put(key, value); // TODO: Check if 0
        }

        public byte[] GetStorageValue(uint160 addr, byte[] key)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState == null ? null : this.storageCache.Get(addr.ToBytes()).Get(key);
        }

        public IContractStateRepository StartTracking()
        {
            ISource<byte[], AccountState> trackAccountStateCache = new WriteCache<AccountState>(this.accountStateCache, WriteCache<AccountState>.CacheType.SIMPLE);
            ISource<byte[], StoredVin> trackVinCache = new WriteCache<StoredVin>(this.vinCache, WriteCache<StoredVin>.CacheType.SIMPLE);
            ISource<byte[], byte[]> trackCodeCache = new WriteCache<byte[]>(this.codeCache, WriteCache< byte[]>.CacheType.SIMPLE);
            MultiCache<ICachedSource<byte[], byte[]>> trackStorageCache = new RealMultiCache(this.storageCache);

            ContractStateRepository ret = new ContractStateRepository(trackAccountStateCache, trackCodeCache, trackStorageCache, trackVinCache);
            ret.parent = this;
            return ret;
        }

        public virtual IContractStateRepository GetSnapshotTo(byte[] stateRoot)
        {
            return this.parent.GetSnapshotTo(stateRoot);
        }

        public virtual void Commit()
        {
            ContractStateRepository parentSync = this.parent == null ? this : this.parent;
            lock(parentSync) {
                this.storageCache.Flush();
                this.codeCache.Flush();
                this.accountStateCache.Flush();
                this.vinCache.Flush();
            }
        }

        public virtual void Rollback()
        {
            // nothing to do, will be GCed
        }

        public virtual byte[] GetRoot()
        {
            throw new Exception("Not supported");
        }

        public virtual void Flush()
        {
            throw new Exception("Not supported");
        }

        public virtual void SyncToRoot(byte[] root)
        {
            throw new Exception("Not supported");
        }

        #region Account Abstraction Layer

        public void TransferBalance(uint160 from, uint160 to, ulong value)
        {
            this.transfers.Add(new TransferInfo
            {
                From = from,
                To = to,
                Value = value
            });
        }

        public IList<TransferInfo> GetTransfers()
        {
            return this.transfers;
        }

        public byte[] GetUnspentHash(uint160 addr)
        {
            AccountState accountState = GetAccountState(addr);
            if (accountState == null || accountState.UnspentHash == null)
                return new byte[0]; // TODO: REPLACE THIS BYTE0 with something

            return accountState.UnspentHash;
        }

        public StoredVin GetUnspent(uint160 address)
        {
            byte[] unspentHash = GetUnspentHash(address);
            return this.vinCache.Get(unspentHash);
        }

        public void SetUnspent(uint160 address, StoredVin vin)
        {
            byte[] vinHash = HashHelper.Keccak256(vin.ToBytes());
            this.vinCache.Put(vinHash, vin);
            AccountState accountState = GetOrCreateAccountState(address);
            accountState.UnspentHash = vinHash;
            this.accountStateCache.Put(address.ToBytes(), accountState);
            this.vinCache.Put(address.ToBytes(), vin);
        }

        #endregion
    }
}