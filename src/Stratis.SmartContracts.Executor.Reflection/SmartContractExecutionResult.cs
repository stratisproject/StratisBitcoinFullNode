using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Carries the output of a smart contract execution.
    /// </summary>
    public sealed class SmartContractExecutionResult : ISmartContractExecutionResult
    {
        /// <inheritdoc/>
        public uint160 NewContractAddress { get; set; }

        /// <inheritdoc/>
        public uint160 To { get; set; }

        /// <inheritdoc/>
        public Exception Exception { get; set; }

        /// <inheritdoc/>
        public bool Revert { get; set; }

        /// <inheritdoc/>
        public ulong FutureRefund { get; set; }

        /// <inheritdoc/>
        public Gas GasConsumed { get; set; }
        
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

        /// <summary>
        /// Contract does not exist, so set the gas units used to a value from the price list and set
        /// a <see cref="SmartContractDoesNotExistException"/>.
        /// </summary>
        internal static ISmartContractExecutionResult ContractDoesNotExist(string methodName)
        {
            var executionResult = new SmartContractExecutionResult
            {
                Exception = new SmartContractDoesNotExistException(methodName),
                GasConsumed = GasPriceList.ContractDoesNotExist()
            };

            return executionResult;
        }

        /// <summary>
        /// Contract validation failed, so set the gas units used to a value from the price list and set
        /// the validation errors in a <see cref="SmartContractValidationException"/>.
        /// </summary>
        public static SmartContractExecutionResult ValidationFailed(SmartContractValidationResult validationResult)
        {
            var executionResult = new SmartContractExecutionResult
            {
                Exception = new SmartContractValidationException(validationResult.Errors),
                GasConsumed = GasPriceList.ContractValidationFailed()
            };

            return executionResult;
        }
    }
}