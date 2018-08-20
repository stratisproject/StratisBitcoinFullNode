using System;
using System.Linq;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class Receipt
    {
        public uint256 PostState { get; }
        public ulong GasUsed { get; }
        public BloomData Bloom { get; }
        public Log[] Logs { get; }

        public Receipt(uint256 postState, ulong gasUsed, BloomData bloom, Log[] logs)
        {
            this.PostState = postState;
            this.GasUsed = gasUsed;
            this.Bloom = bloom;
            this.Logs = logs;
        }

        public byte[] ToBytesRlp()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.PostState.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.GasUsed)),
                RLP.EncodeElement(this.Bloom.ToBytes()),
                RLP.EncodeElement(RLP.EncodeList(this.Logs.Select(x => x.ToBytesRlp()).ToArray()))
            );
        }

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

        public uint256 GetHash()
        {
            return new uint256(HashHelper.Keccak256(ToBytesRlp()));
        }
    }
}
