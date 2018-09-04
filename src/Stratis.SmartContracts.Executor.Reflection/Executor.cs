using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Deserializes raw contract transaction data, dispatches a call to the VM and commits the result to the state repository
    /// </summary>
    public class Executor : ISmartContractExecutor
    {
        private readonly ILogger logger;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IContractState stateSnapshot;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ICallDataSerializer serializer;

        public Executor(ILoggerFactory loggerFactory,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            ICallDataSerializer serializer,
            IContractState stateSnapshot,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.stateSnapshot = stateSnapshot;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.serializer = serializer;
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            // Deserialization can't fail because this has already been through SmartContractFormatRule.
            Result<ContractTxData> callDataDeserializationResult = this.serializer.Deserialize(transactionContext.Data);
            ContractTxData callData = callDataDeserializationResult.Value;

            var gasMeter = new GasMeter(callData.GasLimit);
            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            var context = new TransactionContext(
                transactionContext.TransactionHash,
                transactionContext.BlockHeight,
                transactionContext.CoinbaseAddress,
                transactionContext.Sender,
                transactionContext.TxOutValue);

            var creation = IsCreateContract(callData);

            VmExecutionResult result = creation
                ? this.vm.Create(gasMeter, this.stateSnapshot, callData, context)
                : this.vm.ExecuteMethod(gasMeter, this.stateSnapshot, callData, context);

            var revert = result.ExecutionException != null;

            Transaction internalTransaction = this.transferProcessor.Process(
                this.stateSnapshot,
                creation ? result.NewContractAddress : callData.ContractAddress,
                transactionContext,
                result.InternalTransfers,
                revert);

            (Money fee, List<TxOut> refundTxOuts) = this.refundProcessor.Process(
                callData,
                transactionContext.MempoolFee,
                transactionContext.Sender,
                result.GasConsumed,
                result.ExecutionException);

            var executionResult = new SmartContractExecutionResult
            {
                NewContractAddress = !revert && creation ? result.NewContractAddress : null,
                To = !IsCreateContract(callData) ? callData.ContractAddress : null,
                Exception = result.ExecutionException,
                GasConsumed = result.GasConsumed,
                Return = result.Result,
                InternalTransaction = internalTransaction,
                Fee = fee,
                Refunds = refundTxOuts,
                Logs = result.RawLogs.ToLogs(this.contractPrimitiveSerializer)
            };

            if (revert)
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_FAILED]");

                this.stateSnapshot.Rollback();
            }
            else
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_SUCCEEDED]");

                this.stateSnapshot.Commit();
            }

            return executionResult;
        }

        private static bool IsCreateContract(ContractTxData contractTxData)
        {
            return contractTxData.OpCodeType == (byte) ScOpcodeType.OP_CREATECONTRACT;
        }
    }
}
