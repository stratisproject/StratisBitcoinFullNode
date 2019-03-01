using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public uint256 TransactionHash { get; }

        /// <summary>
        /// Block hash of the block this transaction was contained in.
        /// Needs public set as hash can only be stored after transaction is stored in block.
        /// </summary>
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// Address of the sender of the transaction.
        /// </summary>
        public uint160 From { get; }

        /// <summary>
        /// Contract address sent to in the CALL. Null if CREATE.
        /// </summary>
        public uint160 To { get; }

        /// <summary>
        /// Contract address created in this CREATE. Null if CALL.
        /// </summary>
        public uint160 NewContractAddress { get; }

        /// <summary>
        /// Whether execution completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The result of the execution, serialized as a string.
        /// </summary>
        public string Result { get; }

        /// <summary>
        /// If execution didn't complete successfully, the error will be stored here. 
        /// Could be an exception that occurred inside a contract or a message (e.g. method not found.)
        /// </summary>
        public string ErrorMessage { get; }

        #endregion

        /// <summary>
        /// Creates receipt with both consensus and storage fields and generates bloom.
        /// </summary>
        public Receipt(uint256 postState,
            ulong gasUsed,
            Log[] logs,
            uint256 transactionHash,
            uint160 from,
            uint160 to,
            uint160 newContractAddress,
            bool success,
            string result,
            string errorMessage) 
            : this(postState, gasUsed, logs, BuildBloom(logs), transactionHash, null, from, to, newContractAddress, success, result, errorMessage)
        { }

        /// <summary>
        /// Creates receipt with consensus fields and generates bloom.
        /// </summary>
        public Receipt(
            uint256 postState,
            ulong gasUsed,
            Log[] logs)
            : this(postState, gasUsed, logs, BuildBloom(logs), null, null, null, null, null, false, null, null)
        { }

        /// <summary>
        /// Used for serialization.
        /// </summary>
        private Receipt(
            uint256 postState,
            ulong gasUsed,
            Log[] logs,
            Bloom bloom) 
            : this(postState, gasUsed, logs, bloom, null, null, null, null, null, false, null, null)
        { }

        private Receipt(uint256 postState,
            ulong gasUsed,
            Log[] logs,
            Bloom bloom,
            uint256 transactionHash,
            uint256 blockHash,
            uint160 from,
            uint160 to,
            uint160 newContractAddress,
            bool success,
            string result,
            string errorMessage)
        {
            this.PostState = postState;
            this.GasUsed = gasUsed;
            this.Logs = logs;
            this.Bloom = bloom;
            this.TransactionHash = transactionHash;
            this.BlockHash = blockHash;
            this.From = from;
            this.To = to;
            this.NewContractAddress = newContractAddress;
            this.Success = success;
            this.Result = result;
            this.ErrorMessage = errorMessage;
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
            return new uint256(HashHelper.Keccak256(this.ToConsensusBytesRlp()));
        }

        #endregion

        #region Storage Serialization

        /// <summary>
        /// Parse a whole receipt from stored bytes.
        /// </summary>
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
                new Bloom(innerList[2].RLPData),
                new uint256(innerList[4].RLPData),
                new uint256(innerList[5].RLPData),
                new uint160(innerList[6].RLPData),
                innerList[7].RLPData != null ? new uint160(innerList[7].RLPData) : null,
                innerList[8].RLPData != null ? new uint160(innerList[8].RLPData) : null,
                BitConverter.ToBoolean(innerList[9].RLPData),
                innerList[10].RLPData != null ? Encoding.UTF8.GetString(innerList[10].RLPData) : null,
                innerList[11].RLPData != null ? Encoding.UTF8.GetString(innerList[11].RLPData) : null
            );

            return receipt;
        }

        /// <summary>
        /// Serialize a receipt into bytes to be stored.
        /// </summary>
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
                RLP.EncodeElement(this.NewContractAddress?.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.Success)),
                RLP.EncodeElement(Encoding.UTF8.GetBytes(this.Result ?? "")),
                RLP.EncodeElement(Encoding.UTF8.GetBytes(this.ErrorMessage ?? ""))
            );
        }

        #endregion
    }
}
