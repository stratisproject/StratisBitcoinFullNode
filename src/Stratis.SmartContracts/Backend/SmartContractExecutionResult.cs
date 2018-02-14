using Stratis.SmartContracts.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Carries the output of a smart contract execution.
    /// </summary>
    internal class SmartContractExecutionResult
    {
        /// <summary>
        /// The gas used through execution of the smart contract.
        /// </summary>
        public ulong GasUsed { get; set; }

        /// <summary>
        /// If an object is returned from the method called, it will be stored here.
        /// </summary>
        public object Return { get; set; }

        /// <summary>
        /// If there is an exception during execution, it will be stored here.
        /// TODO: Should this just be an exception?
        /// </summary>
        public SmartContractRuntimeException RuntimeException { get; set; }

        /// <summary>
        /// Whether the state changes made during execution should be reverted. If an exception occurred, then should be true.
        /// </summary>
        public bool Revert { get; set; }

        /// <summary>
        /// A list of transactions made inside the contract call. Should only be one condensing transaction for now.
        /// </summary>
        public List<Transaction> InternalTransactions { get; set; }

        /// <summary>
        /// Used in Ethereum to increase a gas refund.
        /// </summary>
        public ulong FutureRefund { get; set; }

        /// <summary>
        /// If the execution created a new contract, its address will be stored here.
        /// </summary>
        public uint160 NewContractAddress { get; set; }

        public SmartContractExecutionResult()
        {
            this.InternalTransactions = new List<Transaction>();
        }

        /// <summary>
        /// After a contract is executed internally, we will need to merge the results.
        /// </summary>
        /// <param name="another"></param>
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
