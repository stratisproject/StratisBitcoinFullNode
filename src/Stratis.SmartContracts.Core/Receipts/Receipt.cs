using System;
using System.Collections.Generic;
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
        public Bloom Bloom { get; }

        /// <summary>
        /// Logs created during contract execution. 
        /// </summary>
        public Log[] Logs { get; }

        /// <summary>
        /// Creates receipt and generates bloom.
        /// </summary>
        public Receipt(uint256 postState, ulong gasUsed, Log[] logs) 
            : this(postState, gasUsed, logs, BuildBloom(logs))
        {}

        public Receipt(uint256 postState, ulong gasUsed, Log[] logs, Bloom bloom)
        {
            this.PostState = postState;
            this.GasUsed = gasUsed;
            this.Logs = logs;
            this.Bloom = bloom;
        }


        /// <summary>
        /// Get the bits for all of the logs in this receipt.
        /// </summary>
        private static Bloom BuildBloom(Log[] logs)
        {
            var bloom = new Bloom();
            foreach(Log log in logs)
            {
                bloom.Or(log.GetBloom());
            }
            return bloom;
        }

        /// <summary>
        /// Parse a receipt into the consensus data. 
        /// </summary>
        public byte[] ToBytesRlp()
        {
            IList<byte[]> encodedLogs = this.Logs.Select(x => RLP.EncodeElement(x.ToBytesRlp())).ToList();

            return RLP.EncodeList(
                RLP.EncodeElement(this.PostState.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.GasUsed)),
                RLP.EncodeElement(this.Bloom.ToBytes()),
                RLP.EncodeElement(RLP.EncodeList(encodedLogs.ToArray()))
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
                logs,
                new Bloom(innerList[2].RLPData)
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
