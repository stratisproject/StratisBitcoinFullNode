using System.Collections.Generic;

namespace Stratis.SmartContracts.State
{
    internal class AccountState
    {
        public ulong Nonce { get; set; }
        public ulong Balance { get; set; }
        /// <summary>
        /// 32 byte hash of the code deployed at this contract.
        /// Can be used to lookup the actual code in the code table
        /// </summary>
        public byte[] CodeHash { get; set; }

        /// <summary>
        /// 32 byte merkle root of this contract's Patricia trie for storage.
        /// </summary>
        public byte[] StateRoot { get; set; }
    }
}
