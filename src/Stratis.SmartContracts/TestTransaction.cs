using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.SmartContracts
{
    public class TestTransaction
    {
        public ulong BlockNumber { get; set; }

        public uint160 From { get; set; }
        public uint160 To { get; set; }

        public ulong Value { get; set; }
        public ulong GasLimit { get; set; }

        public byte[] Data { get; set; }
        public string ContractTypeName { get; set; }
        public string ContractMethodName { get; set; }
        public object[] Parameters { get; set; }

        public bool IsContractCreation
        {
            get
            {
                return To == null;
            }
        }
    }
}
