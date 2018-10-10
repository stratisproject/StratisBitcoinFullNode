using System;
using NBitcoin;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Handles all of the state for smart contracts. Includes smart contract code, storage, and UTXO balances.
    /// </summary>
    public class StateRepository : IStateRepository
    {
        protected StateRepository parent;
        public ISource<byte[], AccountState> accountStateCache;
        public ISource<byte[], ContractUnspentOutput> vinCache;
        protected ISource<byte[], byte[]> codeCache;
        protected IStorageCaches storageCaches;
        
        protected StateRepository() { }

        public StateRepository(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
            IStorageCaches storageCaches, ISource<byte[], ContractUnspentOutput> vinCache)
        {
            this.Init(accountStateCache, codeCache, storageCaches, vinCache);
        }

        protected void Init(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
            IStorageCaches storageCaches, ISource<byte[], ContractUnspentOutput> vinCache)
        {
            this.accountStateCache = accountStateCache;
            this.codeCache = codeCache;
            this.storageCaches = storageCaches;
            this.vinCache = vinCache;
        }

        public AccountState CreateAccount(uint160 addr)
        {
            AccountState state = new AccountState();
            this.accountStateCache.Put(addr.ToBytes(), state);
            return state;
        }

        public bool IsExist(uint160 addr)
        {
            return this.GetAccountState(addr) != null;
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
                ret = this.CreateAccount(addr);
            }
            return ret;
        }

        public void SetCode(uint160 addr, byte[] code)
        {
            byte[] codeHash = Hashing.HashHelper.Keccak256(code);
            this.codeCache.Put(codeHash, code);
            AccountState accountState = this.GetOrCreateAccountState(addr);
            accountState.CodeHash = codeHash;
            this.accountStateCache.Put(addr.ToBytes(), accountState);
        }

        public byte[] GetCode(uint160 addr)
        {
            byte[] codeHash = this.GetCodeHash(addr);
            return this.codeCache.Get(codeHash);
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            AccountState accountState = this.GetAccountState(addr);
            return accountState != null ? accountState.CodeHash : new byte[0]; // TODO: REPLACE THIS BYTE0 with something
        }

        public void SetStorageValue(uint160 addr, byte[] key, byte[] value)
        {
            this.GetOrCreateAccountState(addr);
            ISource<byte[], byte[]> contractStorage = this.storageCaches.Get(addr.ToBytes());
            contractStorage.Put(key, value); // TODO: Check if 0
        }

        public byte[] GetStorageValue(uint160 addr, byte[] key)
        {
            AccountState accountState = this.GetAccountState(addr);
            return accountState == null ? null : this.storageCaches.Get(addr.ToBytes()).Get(key);
        }

        public string GetContractType(uint160 addr)
        {
            AccountState accountState = this.GetAccountState(addr);
            return accountState != null ? accountState.TypeName : string.Empty;
        }

        public void SetContractType(uint160 addr, string type)
        {
            AccountState accountState = this.GetOrCreateAccountState(addr);
            accountState.TypeName = type;
            this.accountStateCache.Put(addr.ToBytes(), accountState);
        }

        public IStateRepository StartTracking()
        {
            ISource<byte[], AccountState> trackAccountStateCache = new WriteCache<AccountState>(this.accountStateCache, WriteCache<AccountState>.CacheType.SIMPLE);
            ISource<byte[], ContractUnspentOutput> trackVinCache = new WriteCache<ContractUnspentOutput>(this.vinCache, WriteCache<ContractUnspentOutput>.CacheType.SIMPLE);
            ISource<byte[], byte[]> trackCodeCache = new WriteCache<byte[]>(this.codeCache, WriteCache<byte[]>.CacheType.SIMPLE);
            IStorageCaches trackStorageCache = new CachedStorageCaches(this.storageCaches); 
            var stateRepository = new StateRepository(trackAccountStateCache, trackCodeCache, trackStorageCache, trackVinCache)
            {
                parent = this
            };

            return stateRepository;
        }

        /// <summary>
        /// Gets a snaphot of the state repository up until the given state root.
        /// </summary>
        public virtual IStateRepositoryRoot GetSnapshotTo(byte[] stateRoot)
        {
            return this.parent.GetSnapshotTo(stateRoot);
        }

        public virtual void Commit()
        {
            StateRepository parentSync = this.parent == null ? this : this.parent;
            lock (parentSync)
            {
                this.storageCaches.Flush();
                this.codeCache.Flush();
                this.vinCache.Flush();
                this.accountStateCache.Flush();
            }
        }

        public virtual void Rollback()
        {
            // nothing to do, will be GCed
        }

        public virtual void Flush()
        {
            throw new Exception("Not supported");
        }

        #region Account Abstraction Layer

        /// <summary>
        /// Gets the balance for a contract.
        /// Balance = UTXO the contract currently owns, + all the funds it has received, - all the funds it has sent.
        /// 
        /// Note that because the initial transaction will always be coming from a human we don't need to minus if from.
        /// </summary>
        public ulong GetCurrentBalance(uint160 address)
        {
            ulong ret = 0;
            
            ContractUnspentOutput unspent = this.GetUnspent(address);
            if (unspent != null)
                ret += unspent.Value;

            return ret;
        }

        public byte[] GetUnspentHash(uint160 addr)
        {
            AccountState accountState = this.GetAccountState(addr);
            if (accountState == null || accountState.UnspentHash == null)
                return new byte[0]; // TODO: REPLACE THIS BYTE0 with a more meaningful byte array?

            return accountState.UnspentHash;
        }

        public ContractUnspentOutput GetUnspent(uint160 address)
        {
            byte[] unspentHash = this.GetUnspentHash(address);
            return this.vinCache.Get(unspentHash);
        }

        public void ClearUnspent(uint160 address)
        {
            AccountState accountState = this.GetOrCreateAccountState(address);
            accountState.UnspentHash = null;
            this.accountStateCache.Put(address.ToBytes(), accountState);
            // TODO: Delete old unspent from cache?
        }

        public void SetUnspent(uint160 address, ContractUnspentOutput vin)
        {
            byte[] vinHash = Hashing.HashHelper.Keccak256(vin.ToBytes());
            this.vinCache.Put(vinHash, vin);
            AccountState accountState = this.GetOrCreateAccountState(address);
            accountState.UnspentHash = vinHash;
            this.accountStateCache.Put(address.ToBytes(), accountState);
            this.vinCache.Put(address.ToBytes(), vin);
        }

        #endregion
    }
}