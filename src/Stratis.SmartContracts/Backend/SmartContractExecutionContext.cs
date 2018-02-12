using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Information about the current state of the blockchain that is passed into the virtual machine.
    /// </summary>
    internal class SmartContractExecutionContext
    {
        public uint160 ContractAddress { get; set; }
        public uint160 CallerAddress { get; set; }
        public uint160 CoinbaseAddress { get; set; }

        public ulong CallValue { get; set; }
        public ulong GasPrice { get; set; }

        public ulong BlockNumber { get; set; }
        public ulong Difficulty { get; set; }
        public ulong GasLimit { get; set; }

        public string ContractTypeName { get; set; }
        public string ContractMethod { get; set; }
        public object[] Parameters { get; set; }
    }
}
