using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Carries the output of a smart contract execution.
    /// </summary>
    public sealed class SmartContractExecutionResult : ISmartContractExecutionResult
    {
        /// <inheritdoc/>
        public Exception Exception { get; set; }

        /// <inheritdoc/>
        public ulong Fee { get; set; }

        /// <inheritdoc/>
        public ulong FutureRefund { get; set; }

        /// <inheritdoc/>
        public Gas GasConsumed { get; set; }

        /// <inheritdoc/>
        public Transaction InternalTransaction { get; set; }

        public IList<TransferInfo> InternalTransfers { get; set; }

        /// <inheritdoc/>
        public uint160 NewContractAddress { get; set; }

        /// <inheritdoc/>
        public List<TxOut> Refunds { get; set; }

        /// <inheritdoc/>
        public object Return { get; set; }

        /// <inheritdoc/>
        public bool Revert
        {
            get { return this.Exception != null; }
        }

        public SmartContractExecutionResult()
        {
            this.Refunds = new List<TxOut>();
        }

        /// <summary>
        /// After a contract is executed internally, we will need to merge the results.
        /// </summary>
        public void Merge(ISmartContractExecutionResult another)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Contract does not exist, so set the gas units used to a value from the price list and set
        /// a <see cref="SmartContractDoesNotExistException"/>.
        /// </summary>
        internal static ISmartContractExecutionResult ContractDoesNotExist(SmartContractCarrier carrier)
        {
            var executionResult = new SmartContractExecutionResult
            {
                Exception = new SmartContractDoesNotExistException(carrier.MethodName),
                GasConsumed = GasPriceList.ContractDoesNotExist()
            };

            return executionResult;
        }

        /// <summary>
        /// Contract validation failed, so set the gas units used to a value from the price list and set
        /// the validation errors in a <see cref="SmartContractValidationException"/>.
        /// </summary>
        public static SmartContractExecutionResult ValidationFailed(SmartContractCarrier carrier, SmartContractValidationResult validationResult)
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