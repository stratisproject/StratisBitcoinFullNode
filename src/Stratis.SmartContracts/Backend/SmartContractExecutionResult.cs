using Stratis.SmartContracts.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.Backend
{
    internal class SmartContractExecutionResult
    {
        public ulong GasUsed { get; set; }
        public object Return { get; set; }
        public SmartContractRuntimeException RuntimeException { get; set; }
        public bool Revert { get; set; }
        public List<Transaction> InternalTransactions { get; set; }
        public ulong FutureRefund { get; set; }
        public uint160 NewContractAddress { get; set; }

        public SmartContractExecutionResult()
        {
            this.InternalTransactions = new List<Transaction>();
        }

        public void Merge(SmartContractExecutionResult another)
        {
            throw new NotImplementedException();
            if (another.RuntimeException == null && !another.Revert)
            {
                this.FutureRefund += another.FutureRefund;
                this.InternalTransactions.AddRange(another.InternalTransactions);
            }
        }

    }
}
