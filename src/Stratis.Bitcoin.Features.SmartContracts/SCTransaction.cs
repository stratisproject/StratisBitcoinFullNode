using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCTransaction
    {
        public uint VmVersion { get; set; }
        public ulong GasLimit { get; set; }
        public ulong GasPrice { get; set; }
        public ulong Value { get; set; }
        public byte[] ContractCode { get; set; }
        public OpcodeType OpCodeType { get; set; }
        
        public SCTransaction() { }

        /// <summary>
        /// So heinous! Can adjust later.
        /// </summary>
        /// <param name="txOut"></param>
        public SCTransaction(TxOut txOut)
        {
            Value = txOut.Value;
            var bytes = txOut.ScriptPubKey.ToBytes();
            OpCodeType = (OpcodeType) bytes.LastOrDefault();
            VmVersion = BitConverter.ToUInt32(bytes.Take(4).ToArray(), 0);
            GasLimit = BitConverter.ToUInt64(bytes.Skip(4).Take(8).ToArray(), 0);
            GasPrice = BitConverter.ToUInt64(bytes.Skip(12).Take(8).ToArray(), 0);
            ContractCode = bytes.Skip(20).SkipLast(1).ToArray();
        }

        public IEnumerable<byte> ToBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(VmVersion));
            bytes.AddRange(BitConverter.GetBytes(GasLimit));
            bytes.AddRange(BitConverter.GetBytes(GasPrice));
            bytes.AddRange(ContractCode);
            bytes.Add((byte)this.OpCodeType);
            return bytes;
        }
    }
}
