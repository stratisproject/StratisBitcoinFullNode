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

        public HashSet<uint256> DeleteAccounts { get; set; }
        //public List<InternalTransaction> InternalTransactions { get; set; }
        public HashSet<uint256> TouchedAccounts { get; set; }
        // TODO: List of log info here?
        // TODO: List of CallCreate?
        public ulong FutureRefund { get; set; }

        public uint160 NewContractAddress { get; set; }

        public SmartContractExecutionResult()
        {
            DeleteAccounts = new HashSet<uint256>();
            //InternalTransactions = new List<InternalTransaction>();
        }

        public void Merge(SmartContractExecutionResult another)
        {
            //InternalTransactions.AddRange(another.InternalTransactions);
            if (another.RuntimeException == null && !another.Revert)
            {
                DeleteAccounts.UnionWith(another.DeleteAccounts);
                FutureRefund += another.FutureRefund;
                another.TouchedAccounts.UnionWith(another.TouchedAccounts);
            }
        }

    }
}
