using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts.Trie
{
    public class AccountStateTrie : ISource<uint160, AccountState>
    {
        private PatriciaTrie trie;

        public AccountStateTrie(PatriciaTrie trie)
        {
            this.trie = trie;
        }

        public AccountState Get(uint160 key)
        {
            byte[] bytes = this.trie.Get(key.ToBytes());
            return new AccountState(bytes);
        }

        public void Put(uint160 key, AccountState val)
        {
            this.trie.Put(key.ToBytes(), val.ToBytes());
        }

        public void Delete(uint160 key)
        {
            this.trie.Delete(key.ToBytes());
        }

        public bool Flush()
        {
            this.trie.Flush();
            return true;
        }

        public void SetRoot(byte[] root)
        {
            this.trie.SetRoot(root);
        }

        public byte[] GetRootHash()
        {
            return this.trie.GetRootHash();
        }
    }
}
