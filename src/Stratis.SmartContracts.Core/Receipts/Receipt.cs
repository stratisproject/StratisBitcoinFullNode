using System;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class Receipt
    {
        public uint256 TransactionId { get; }

        public uint256 BlockHash { get; }

        public uint160 Sender { get; }

        public uint160 To { get; }

        public uint160 NewContractAddress { get; }

        public ulong GasUsed { get; }

        public bool Success { get; }

        public byte[] ReturnValue { get; }

        public Receipt(
            uint256 transactionId,
            uint256 blockHash,
            uint160 sender,
            uint160 to,
            uint160 newContractAddress,
            ulong gasUsed,
            bool success,
            byte[] returnValue)
        {
            this.TransactionId = transactionId;
            this.BlockHash = blockHash;
            this.Sender = sender;
            this.To = to;
            this.NewContractAddress = newContractAddress;
            this.GasUsed = gasUsed;
            this.Success = success;
            this.ReturnValue = returnValue;
        }

        public byte[] ToBytesRlp()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.TransactionId.ToBytes()),
                RLP.EncodeElement(this.BlockHash.ToBytes()),
                RLP.EncodeElement(this.Sender.ToBytes()),
                RLP.EncodeElement(this.To?.ToBytes()),
                RLP.EncodeElement(this.NewContractAddress?.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.GasUsed)),
                RLP.EncodeElement(BitConverter.GetBytes(this.Success)),
                RLP.EncodeElement(this.ReturnValue)
            );
        }

        public static Receipt FromBytesRlp(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];
            return new Receipt(
                new uint256(innerList[0].RLPData),
                new uint256(innerList[1].RLPData),
                new uint160(innerList[2].RLPData),
                innerList[3].RLPData == null ? null : new uint160(innerList[3].RLPData),
                innerList[4].RLPData == null ? null : new uint160(innerList[4].RLPData),
                BitConverter.ToUInt64(innerList[5].RLPData),
                BitConverter.ToBoolean(innerList[6].RLPData),
                innerList[7].RLPData
            );
        }

        public uint256 GetHash()
        {
            return new uint256(HashHelper.Keccak256(ToBytesRlp()));
        }
    }
}
