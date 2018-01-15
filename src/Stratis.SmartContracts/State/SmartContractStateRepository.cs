using DBreeze;
using DBreeze.Utils;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Hashing;
using Stratis.SmartContracts.Trie;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    // TODOs:

    // -Handle object serialization. At the moment trie only retrieves bytes

    // -Experiment with using only one db for everything - if it's all hashed before it goes in the db
    //  then theoretically we only need 1 KV store

    public class SmartContractStateRepository : ISmartContractStateRepository
    {
        private DBreezeEngine engine = null;
        private AccountStateTrie accountStateTrie;
        private Dictionary<uint160, PatriciaTrie> contractStorageTries;

        private const string AccountStateTable = "AccountState";
        private const string CodeTable = "Code";
        private const string DbLocation = @"C:\temp";

        public SmartContractStateRepository(string dbLocation = null)
        {
            this.engine = new DBreezeEngine(dbLocation ?? DbLocation);
            this.accountStateTrie = new AccountStateTrie(new PatriciaTrie(new DBreezeByteStore(this.engine, AccountStateTable)));
            this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        }

        protected SmartContractStateRepository(DBreezeEngine engine, AccountStateTrie accountStateTrie)
        {
            this.engine = engine;
            this.accountStateTrie = accountStateTrie;
            this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        }

        public ISmartContractStateRepository StartTracking()
        {
            var bytes = accountStateTrie.GetRootHash();
            return new SmartContractStateRepository(this.engine, new AccountStateTrie(new PatriciaTrie(new DBreezeByteStore(this.engine, AccountStateTable), this.accountStateTrie.GetRootHash())));
        }

        /// <summary>
        /// Empty database
        /// </summary>
        public void Refresh()
        {
            using (var t = engine.GetTransaction())
            {
                // TODO: Once the keys are all stored in a single table, just wipe out that table
                // At the moment each contract has a separate table that is storing things.
                t.RemoveAllKeys(AccountStateTable, true);
                t.RemoveAllKeys(CodeTable, true);
            }
        }

        public void SetCode(uint160 address, byte[] code)
        {
            AccountState accountState = GetAccountState(address);
            accountState.CodeHash = HashHelper.Keccak256(code);
            this.accountStateTrie.Put(address, accountState);
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                t.Insert<byte[], byte[]>(CodeTable, accountState.CodeHash, code);
                t.Commit();
            }
        }

        public byte[] GetCode(uint160 address)
        {
            AccountState accountState = GetAccountState(address);
            using (DBreeze.Transactions.Transaction t = this.engine.GetTransaction())
            {
                DBreeze.DataTypes.Row<byte[], byte[]> row = t.Select<byte[], byte[]>(CodeTable, accountState.CodeHash);

                if (row.Exists)
                    return row.Value;

                return null;
            }
        }

        public AccountState CreateAccount(uint160 address)
        {
            var accountState = new AccountState();
            this.accountStateTrie.Put(address, accountState);
            return accountState;
        }

        private AccountState GetOrCreateAccountState(uint160 address)
        {
            AccountState accountState = GetAccountState(address);
            if (accountState != null)
                return accountState;

            return CreateAccount(address);
        }

        private AccountState GetAccountState(uint160 address)
        {
            return this.accountStateTrie.Get(address);
        }

        public void SetObject<T>(uint160 address, object key, T toStore)
        {
            // TODO: Can be optimised. We're getting account state twice.
            PatriciaTrie trie = GetContractStorageTrie(address);

            //var test = trie.GetRootHash();

            trie.Put((byte[])key, (byte[])(object)toStore);
            
            //accountState.StateRoot = trie.GetRootHash();
            //this.accountStateTrie.Put(address, accountState);
        }

        public T GetObject<T>(uint160 address, object key)
        {
            return (T)(object) GetContractStorageTrie(address).Get((byte[])key);
        }

        private PatriciaTrie GetContractStorageTrie(uint160 address)
        {
            if (this.contractStorageTries.ContainsKey(address))
                return this.contractStorageTries[address];

            AccountState accountState = GetAccountState(address);
            PatriciaTrie contractStorageTrie = new PatriciaTrie(new DBreezeByteStore(this.engine, address.ToString()), accountState.StateRoot);
            this.contractStorageTries.Add(address, contractStorageTrie);
            return contractStorageTrie;
        }

        public void Commit()
        {
            foreach(KeyValuePair<uint160,PatriciaTrie> kvp in this.contractStorageTries)
            {
                kvp.Value.Flush();
                AccountState accountState = GetAccountState(kvp.Key);
                accountState.StateRoot = kvp.Value.GetRootHash();
                this.accountStateTrie.Put(kvp.Key, accountState);
            }
            this.accountStateTrie.Flush();
            this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        }

        public void Rollback()
        {
            // Do nothing - let garbage collection take care of it.
        }

        public void LoadSnapshot(byte[] root)
        {
            this.accountStateTrie.SetRoot(root);
            this.contractStorageTries = new Dictionary<uint160, PatriciaTrie>();
        }

        public byte[] GetRoot()
        {
            return this.accountStateTrie.GetRootHash();
        }

    }
}