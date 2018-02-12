using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Nethereum.RLP;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    /// <summary>
    /// Each contract's balance is stored in one of these. 
    /// It is a reference to an unspent output.
    /// </summary>
    public class StoredVin
    {
        /// <summary>
        /// Hash of the transaction that sent this contract funds.
        /// </summary>
        public uint256 Hash { get; set; }

        /// <summary>
        /// Index of the output inside the transaction that sent shit contract funds.
        /// </summary>
        public uint Nvout { get; set; }

        /// <summary>
        /// Amount sent in the referenced output.
        /// </summary>
        public ulong Value { get; set; }

        /// <summary>
        /// TODO: Delete unless we actually use it. I think we can avoid it.
        /// -Jordan 12/02/2018
        /// </summary>
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
