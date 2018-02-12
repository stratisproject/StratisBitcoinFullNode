﻿using System;
using System.Linq;
using Nethereum.RLP;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// A contract's state.
    /// </summary>
    public class AccountState
    {
        /// <summary>
        /// 32 byte hash of the code deployed at this contract.
        /// Can be used to lookup the actual code in the code table
        /// </summary>
        public byte[] CodeHash { get; set; }

        /// <summary>
        /// 32 byte merkle root of this contract's Patricia trie for storage.
        /// </summary>
        public byte[] StateRoot { get; set; }

        /// <summary>
        /// 32 byte hash of the unspent output currently being used by this contract.
        /// </summary>
        public byte[] UnspentHash { get; set; }

        public AccountState(){}

        #region Serialization

        public AccountState(byte[] bytes) : this()
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];
            this.CodeHash = innerList[0].RLPData;
            this.StateRoot = innerList[1].RLPData;
            this.UnspentHash = innerList[2].RLPData;
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.CodeHash ?? new byte[0]),
                RLP.EncodeElement(this.StateRoot ?? new byte[0]),
                RLP.EncodeElement(this.UnspentHash ?? new byte[0])
                );
        }

        #endregion
    }
}
