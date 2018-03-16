using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Handles all of the state for smart contracts. Includes smart contract code, storage, and UTXO balances.
    /// </summary>
    public class ContractStateRepository : IContractStateRepository
    {
        protected ContractStateRepository parent;
        public ISource<byte[], AccountState> accountStateCache;
        public ISource<byte[], ContractUnspentOutput> vinCache;
        protected ISource<byte[], byte[]> codeCache;
        protected MultiCache<ICachedSource<byte[], byte[]>> storageCache;
        public List<TransferInfo> Transfers { get; private set; }
        public SmartContractCarrier CurrentCarrier { get; set; }

        protected ContractStateRepository() { }

        public ContractStateRepository(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                      MultiCache<ICachedSource<byte[], byte[]>> storageCache, ISource<byte[], ContractUnspentOutput> vinCache)
        {
            this.Init(accountStateCache, codeCache, storageCache, vinCache);
        }

        protected void Init(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                    MultiCache<ICachedSource<byte[], byte[]>> storageCache, ISource<byte[], ContractUnspentOutput> vinCache)
        {
            this.accountStateCache = accountStateCache;
            this.codeCache = codeCache;
            this.storageCache = storageCache;
            this.vinCache = vinCache;
            this.Transfers = new List<TransferInfo>();
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

        public void Delete(uint160 addr)
        {
            this.accountStateCache.Delete(addr.ToBytes());
            this.storageCache.Delete(addr.ToBytes());
        }

        public void SetCode(uint160 addr, byte[] code)
        {
            byte[] codeHash = HashHelper.Keccak256(code);
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
            ISource<byte[], byte[]> contractStorage = this.storageCache.Get(addr.ToBytes());
            contractStorage.Put(key, value); // TODO: Check if 0
        }

        public byte[] GetStorageValue(uint160 addr, byte[] key)
        {
            AccountState accountState = this.GetAccountState(addr);
            return accountState == null ? null : this.storageCache.Get(addr.ToBytes()).Get(key);
        }

        public IContractStateRepository StartTracking()
        {
            ISource<byte[], AccountState> trackAccountStateCache = new WriteCache<AccountState>(this.accountStateCache, WriteCache<AccountState>.CacheType.SIMPLE);
            ISource<byte[], ContractUnspentOutput> trackVinCache = new WriteCache<ContractUnspentOutput>(this.vinCache, WriteCache<ContractUnspentOutput>.CacheType.SIMPLE);
            ISource<byte[], byte[]> trackCodeCache = new WriteCache<byte[]>(this.codeCache, WriteCache<byte[]>.CacheType.SIMPLE);
            MultiCache<ICachedSource<byte[], byte[]>> trackStorageCache = new RealMultiCache(this.storageCache);

            ContractStateRepository ret = new ContractStateRepository(trackAccountStateCache, trackCodeCache, trackStorageCache, trackVinCache);
            ret.parent = this;
            ret.Transfers = new List<TransferInfo>(this.Transfers);
            ret.CurrentCarrier = this.CurrentCarrier;
            return ret;
        }

        public virtual ContractStateRepositoryRoot GetSnapshotTo(byte[] stateRoot)
        {
            return this.parent.GetSnapshotTo(stateRoot);
        }

        public virtual void Commit()
        {
            if (this.parent != null)
                this.parent.Transfers.AddRange(this.Transfers.Where(x => !this.parent.Transfers.Contains(x)));

            ContractStateRepository parentSync = this.parent == null ? this : this.parent;
            lock (parentSync)
            {
                this.storageCache.Flush();
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

        public void TransferBalance(uint160 from, uint160 to, ulong value)
        {
            this.Transfers.Add(new TransferInfo
            {
                From = from,
                To = to,
                Value = value
            });
        }

        /// <summary>
        /// Gets the balance for a contract.
        /// Balance = UTXO the contract currently owns, + all the funds it has received, - all the funds it has sent.
        /// 
        /// Note that because the initial transaction will always be coming from a human we don't need to minus if from.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public ulong GetCurrentBalance(uint160 address)
        {
            ulong ret = 0;
            if (this.CurrentCarrier?.To == address)
                ret += this.CurrentCarrier.TxOutValue;

            ContractUnspentOutput unspent = this.GetUnspent(address);
            if (unspent != null)
                ret += unspent.Value;

            foreach (TransferInfo transfer in this.Transfers.Where(x => x.To == address))
            {
                ret += transfer.Value;
            }

            foreach (TransferInfo transfer in this.Transfers.Where(x => x.From == address))
            {
                ret -= transfer.Value;
            }

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

        public void SetUnspent(uint160 address, ContractUnspentOutput vin)
        {
            byte[] vinHash = HashHelper.Keccak256(vin.ToBytes());
            this.vinCache.Put(vinHash, vin);
            AccountState accountState = this.GetOrCreateAccountState(address);
            accountState.UnspentHash = vinHash;
            this.accountStateCache.Put(address.ToBytes(), accountState);
            this.vinCache.Put(address.ToBytes(), vin);
        }

        #endregion
    }
}