﻿using System;
using System.Linq;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Holds information about the result of smart contract transaction executions. 
    /// </summary>
    public class Receipt
    {
        /// <summary>
        /// State root after smart contract execution. Note that if contract failed this will be the same as previous state.
        /// </summary>
        public uint256 PostState { get; }

        /// <summary>
        /// Gas consumed in this smart contract execution.
        /// </summary>
        public ulong GasUsed { get; }

        /// <summary>
        /// Bloom data representing all of the indexed logs contained inside this receipt.
        /// </summary>
        public BloomData Bloom { get; }

        /// <summary>
        /// Logs created during contract execution. 
        /// </summary>
        public Log[] Logs { get; }

        public Receipt(uint256 postState, ulong gasUsed, BloomData bloom, Log[] logs)
        {
            this.PostState = postState;
            this.GasUsed = gasUsed;
            this.Bloom = bloom;
            this.Logs = logs;
        }

        /// <summary>
        /// Parse a receipt into the consensus data. 
        /// </summary>
        public byte[] ToBytesRlp()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.PostState.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.GasUsed)),
                RLP.EncodeElement(this.Bloom.ToBytes()),
                RLP.EncodeElement(RLP.EncodeList(this.Logs.Select(x => x.ToBytesRlp()).ToArray()))
            );
        }

        /// <summary>
        /// Parse a Receipt from the stored consensus data. 
        /// </summary>
        public static Receipt FromBytesRlp(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection) list[0];

            RLPCollection logList = RLP.Decode(innerList[3].RLPData);
            RLPCollection innerLogList = (RLPCollection)logList[0];
            Log[] logs = innerLogList.Select(x => Log.FromBytesRlp(x.RLPData)).ToArray();

            return new Receipt(
                new uint256(innerList[0].RLPData),
                BitConverter.ToUInt64(innerList[1].RLPData),
                new BloomData(innerList[2].RLPData),
                logs
            );
        }

        /// <summary>
        /// Get a hash of the entire receipt.
        /// </summary>
        public uint256 GetHash()
        {
            return new uint256(HashHelper.Keccak256(ToBytesRlp()));
        }
    }
}
