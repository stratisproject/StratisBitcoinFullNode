using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Hashing;

namespace Stratis.SmartContracts
{
    public class SmartContractTransaction
    {
        public uint VmVersion { get; set; }
        public ulong GasLimit { get; set; }
        public ulong GasPrice { get; set; }
        public ulong Value { get; set; }
        public ulong TotalGas
        {
            get
            {
                return this.GasPrice * this.GasLimit;
            }
        }
        public object[] Parameters { get; set; }

        public byte[] ContractCode { get; set; }

        public uint160 To { get; set; }
        public uint160 Sender { get; set; }
        public string MethodName { get; set; }

        public OpcodeType OpCodeType { get; set; }

        public uint256 Hash { get; set; }
        public uint Nvout { get; set; }
        
        public SmartContractTransaction() { }

        /// <summary>
        /// So heinous! Can adjust later. Also TODO: parameters
        /// </summary>
        /// <param name="txOut"></param>
        public SmartContractTransaction(TxOut txOut, Transaction transaction)
        {
            var bytes = txOut.ScriptPubKey.ToBytes();
            OpCodeType = (OpcodeType)bytes.LastOrDefault();
            Value = txOut.Value;
            VmVersion = BitConverter.ToUInt32(bytes.Take(4).ToArray(), 0);
            GasLimit = BitConverter.ToUInt64(bytes.Skip(4).Take(8).ToArray(), 0);
            GasPrice = BitConverter.ToUInt64(bytes.Skip(12).Take(8).ToArray(), 0);

            if (OpCodeType == OpcodeType.OP_CREATECONTRACT)
            {
                ContractCode = bytes.Skip(20).SkipLast(1).ToArray();
            }
            else if (OpCodeType == OpcodeType.OP_CALLCONTRACT)
            {
                To = new uint160(bytes.Skip(20).Take(20).ToArray());
                MethodName = Encoding.UTF8.GetString(bytes.Skip(40).SkipLast(1).ToArray());
            }
            Nvout = Convert.ToUInt32(transaction.Outputs.IndexOf(txOut));
            Hash = transaction.GetHash();
            //From = GetSenderAddress();
        }

        public IEnumerable<byte> ToBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(VmVersion));
            bytes.AddRange(BitConverter.GetBytes(GasLimit));
            bytes.AddRange(BitConverter.GetBytes(GasPrice));
            if (this.OpCodeType == OpcodeType.OP_CREATECONTRACT)
            {
                bytes.AddRange(ContractCode);
            }
            else
            {
                bytes.AddRange(To.ToBytes());
                bytes.AddRange(Encoding.UTF8.GetBytes(MethodName));
            }
            bytes.Add((byte)this.OpCodeType);
            return bytes;
        }

        /// <summary>
        /// Could put this on the 'Transaction' object in NBitcoin if allowed
        /// </summary>
        /// <returns></returns>
        public uint160 GetNewContractAddress()
        {
            return new uint160(HashHelper.Keccak256(this.Hash.ToBytes()).Take(20).ToArray());
        }

        //public uint160 GetSenderAddress()
        //{
        //    throw new NotImplementedException(); // TODO: Full node dev?
        //    //return 100;
        //}
    }
}
