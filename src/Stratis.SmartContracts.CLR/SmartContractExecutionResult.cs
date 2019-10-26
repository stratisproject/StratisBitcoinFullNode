using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Carries the output of a smart contract execution.
    /// </summary>
    public sealed class SmartContractExecutionResult : IContractExecutionResult
    {
        /// <inheritdoc/>
        public uint160 NewContractAddress { get; set; }

        /// <inheritdoc/>
        public uint160 To { get; set; }

        /// <inheritdoc/>
        public ContractErrorMessage ErrorMessage { get; set; }

        /// <inheritdoc/>
        public bool Revert { get; set; }

        /// <inheritdoc/>
        public ulong GasConsumed { get; set; }
        
        /// <inheritdoc/>
        public object Return { get; set; }

        /// <inheritdoc/>
        public Transaction InternalTransaction { get; set; }

        /// <inheritdoc/>
        public ulong Fee { get; set; }

        /// <inheritdoc/>
        public TxOut Refund { get; set; }

        /// <inheritdoc />
        public IList<Log> Logs { get; set; }

        public SmartContractExecutionResult()
        {
            this.Logs = new List<Log>();
        }

    }
}