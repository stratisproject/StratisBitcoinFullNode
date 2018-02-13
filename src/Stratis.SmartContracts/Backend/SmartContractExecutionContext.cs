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
        internal SmartContractExecutionContext(
            Block block, 
            Message message, 
            ulong gasPrice,
            object[] parameters)
        {
            Block = block;
            Message = message;
            GasPrice = gasPrice;
            Parameters = parameters;
        }
        public ulong GasPrice { get; }

        public object[] Parameters { get; }

        public Message Message { get; }

        public Block Block { get; }
    }
}
