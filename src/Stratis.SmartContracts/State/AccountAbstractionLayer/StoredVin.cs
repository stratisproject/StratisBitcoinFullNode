using System;
using NBitcoin;
using Nethereum.RLP;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class StoredVin
    {
        public uint256 Hash { get; set; }
        public uint Nvout { get; set; }
        public ulong Value { get; set; }
        public byte Alive { get; set; }

        public StoredVin() { }

        public StoredVin(byte[] bytes)
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];
            this.Hash = new uint256(innerList[0].RLPData);
            this.Nvout = BitConverter.ToUInt32(innerList[1].RLPData, 0);
            this.Value = BitConverter.ToUInt64(innerList[2].RLPData, 0);
            this.Alive = innerList[3].RLPData[0];
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.Hash.ToBytes()),
                RLP.EncodeElement(BitConverter.GetBytes(this.Nvout)),
                RLP.EncodeElement(BitConverter.GetBytes(this.Value)),
                RLP.EncodeElement(new byte[] { this.Alive })
                );
        }
    }
}
