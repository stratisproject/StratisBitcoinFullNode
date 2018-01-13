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
    public class SmartContractStateRepository : ISmartContractStateRepository
    {
        private DBreezeEngine engine = null;
        private AccountStateTrie accountStateTrie; 

        private const string AccountStateTable = "AccountState";
        private const string CodeTable = "Code";
        private const string DbLocation = @"C:\temp";

        public SmartContractStateRepository(string dbLocation = null)
        {
            this.engine = new DBreezeEngine(dbLocation ?? DbLocation);
            this.accountStateTrie = new AccountStateTrie(new Trie.Trie(new DBreezeByteStore(this.engine, AccountStateTable)));

            //// Feel like this is really bad. TODO: Is there any better way of doing this?
            //DBreeze.Utils.CustomSerializator.ByteArraySerializator = (object o) =>
            //{
            //    if (o is uint160)
            //        return ((uint160)o).ToBytes();
            //    if (o is Address)
            //        return ((Address)o).ToUint160().ToBytes();

            //    return NetJSON.NetJSON.Serialize(o).To_UTF8Bytes();
            //};
            //DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = (byte[] bt, Type t) =>
            //{
            //    if (t == typeof(uint160))
            //        return new uint160(new uint160(bt));
            //    if (t == typeof(Address))
            //        return new Address(new uint160(bt));

            //    return NetJSON.NetJSON.Deserialize(t, bt.UTF8_GetString());
            //};
        }

        /// <summary>
        /// Empty database
        /// </summary>
        public void Refresh()
        {
            using (var t = engine.GetTransaction())
            {
                var allAccounts = t.SelectDictionary<byte[], byte[]>(AccountStateTable);
                foreach(var key in allAccounts.Keys)
                {
                    t.RemoveAllKeys(new uint160(key).ToString(), true);
                }
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
            AccountState accountState = GetAccountState(address);
            Trie.Trie contractStorageTrie = new Trie.Trie(new DBreezeByteStore(this.engine, address.ToString()));
            contractStorageTrie.SetRoot(accountState.StateRoot);
            contractStorageTrie.Put((byte[])key, (byte[]) (object) toStore);
            contractStorageTrie.Flush(); // do later on in future
            accountState.StateRoot = contractStorageTrie.GetRootHash();
            this.accountStateTrie.Put(address, accountState);
        }

        public T GetObject<T>(uint160 address, object key)
        {
            AccountState accountState = GetAccountState(address);
            Trie.Trie contractStorageTrie = new Trie.Trie(new DBreezeByteStore(this.engine, address.ToString()));
            contractStorageTrie.SetRoot(accountState.StateRoot);
            return (T) (object) contractStorageTrie.Get((byte[])key);
        }

        public void Rollback(uint256 hash)
        {
            throw new NotImplementedException();
        }

        public void Commit()
        {
            this.accountStateTrie.Flush();
        }
    }
}