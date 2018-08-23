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
        #region Consensus Properties

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
        #endregion

        #region Storage Properties

        /// <summary>
        /// Hash of the transaction.
        /// </summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>
        /// Block hash of the block this transaction was contained in.
        /// </summary>
        public uint256 BlockHash { get; private set; }

        /// <summary>
        /// Address of the sender of the transaction.
        /// </summary>
        public uint160 From { get; private set; }

        /// <summary>
        /// Contract address sent to in the CALL. Null if CREATE.
        /// </summary>
        public uint160 To { get; private set; }

        /// <summary>
        /// Contract address created in this CREATE. Null if CALL.
        /// </summary>
        public uint160 NewContractAddress { get; private set; }

        #endregion

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
        /// Set the extra properties to be stored as part of the receipt, some of which may only be available after block execution is complete.
        /// </summary>
        public void SetStorageProperties(uint256 transactionHash, uint256 blockHash, uint160 from, uint160 to, uint160 newContractAddress)
        {
            this.TransactionHash = transactionHash;
            this.BlockHash = blockHash;
            this.From = from;
            this.To = to;
            this.NewContractAddress = newContractAddress;
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

        #region Consensus Serialization

        /// <summary>
        /// Parse a receipt into the consensus data. 
        /// </summary>
        public byte[] ToConsensusBytesRlp()
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
        public static Receipt FromConsensusBytesRlp(byte[] bytes)
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
        /// Get a hash of the consensus receipt.
        /// </summary>
        public uint256 GetHash()
        {
            return new uint256(HashHelper.Keccak256(ToConsensusBytesRlp()));
        }

        #endregion

        #region Storage Serialization

        public static Receipt FromStorageBytesRlp(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];

            RLPCollection logList = RLP.Decode(innerList[3].RLPData);
            RLPCollection innerLogList = (RLPCollection)logList[0];
            Log[] logs = innerLogList.Select(x => Log.FromBytesRlp(x.RLPData)).ToArray();

            var receipt = new Receipt(
                new uint256(innerList[0].RLPData),
                BitConverter.ToUInt64(innerList[1].RLPData),
                logs,
                new Bloom(innerList[2].RLPData)
            );
            receipt.SetStorageProperties(
                new uint256(innerList[4].RLPData),
                new uint256(innerList[5].RLPData),
                new uint160(innerList[6].RLPData),
                innerList[7].RLPData != null ? new uint160(innerList[7].RLPData) : null,
                innerList[8].RLPData != null ? new uint160(innerList[8].RLPData) : null
            );
            return receipt;
        }

        public byte[] ToStorageBytesRlp()
        {
            IList<byte[]> encodedLogs = this.Logs.Select(x => RLP.EncodeElement(x.ToBytesRlp())).ToList();

            return RLP.EncodeList(
                RLP.EncodeElement(this.PostState.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.GasUsed)),
                RLP.EncodeElement(this.Bloom.ToBytes()),
                RLP.EncodeElement(RLP.EncodeList(encodedLogs.ToArray())),
                RLP.EncodeElement(this.TransactionHash.ToBytes()),
                RLP.EncodeElement(this.BlockHash.ToBytes()),
                RLP.EncodeElement(this.From.ToBytes()),
                RLP.EncodeElement(this.To?.ToBytes()),
                RLP.EncodeElement(this.NewContractAddress?.ToBytes())
            );
        }

        #endregion
    }
}
