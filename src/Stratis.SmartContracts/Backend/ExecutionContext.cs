using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Information about the current state of the blockchain that can be accessed in the virtual machine.
    /// </summary>
    internal class ExecutionContext
    {
        // If it will somehow improve performance in future to make these a UINT180, then do so
        public uint160 ContractAddress { get; set; }
        public uint160 CallerAddress { get; set; }
        public uint160 CoinbaseAddress { get; set; }

        public ulong CallValue { get; set; }
        public ulong GasPrice { get; set; }
        public byte[] CallData { get; set; }

        public uint256 BlockHash { get; set; }
        public ulong BlockNumber { get; set; }
        public ulong Difficulty { get; set; }
        public ulong GasLimit { get; set; }

        // Experiments - these are brand new
        public string ContractTypeName { get; set; }
        public string ContractMethod { get; set; }
        public object[] Parameters { get; set; }
    }
}
