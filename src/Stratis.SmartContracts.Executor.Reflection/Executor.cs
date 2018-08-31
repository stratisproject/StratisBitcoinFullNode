using System.Collections.Generic;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Deserializes raw contract transaction data, creates an external create/call message, and applies the message to the state.
    /// </summary>
    public class Executor : ISmartContractExecutor
    {
        private readonly ILogger logger;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IContractStateRoot stateRoot;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ICallDataSerializer serializer;
        private readonly Network network;
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly IAddressGenerator addressGenerator;

        public Executor(ILoggerFactory loggerFactory,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            ICallDataSerializer serializer,
            IContractStateRoot stateRoot,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm,
            IAddressGenerator addressGenerator,
            Network network,
            InternalTransactionExecutorFactory internalTransactionExecutorFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.stateRoot = stateRoot;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.serializer = serializer;
            this.addressGenerator = addressGenerator;
            this.network = network;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            // Deserialization can't fail because this has already been through SmartContractFormatRule.
            Result<ContractTxData> callDataDeserializationResult = this.serializer.Deserialize(transactionContext.Data);
            ContractTxData callData = callDataDeserializationResult.Value;

            var creation = callData.IsCreateContract;

            var block = new Block(
                transactionContext.BlockHeight,
                transactionContext.CoinbaseAddress.ToAddress(this.network)
            );

            var state = new State(
                this.contractPrimitiveSerializer,
                this.internalTransactionExecutorFactory,
                this.vm,
                this.stateRoot,
                block, 
                this.network,
                transactionContext.TxOutValue,
                transactionContext.TransactionHash,
                this.addressGenerator,
                callData.GasLimit);

            StateTransitionResult result;

            if (creation)
            {
                var message = new ExternalCreateMessage(
                    transactionContext.Sender,
                    transactionContext.TxOutValue,
                    callData.GasLimit,
                    callData.ContractExecutionCode,
                    callData.MethodParameters
                );

                result = state.Apply(message);
            }
            else
            {
                var message = new ExternalCallMessage(
                        callData.ContractAddress,
                        transactionContext.Sender,
                        transactionContext.TxOutValue,
                        callData.GasLimit,
                        new MethodCall(callData.MethodName, callData.MethodParameters)
                );

                result = state.Apply(message);
            }

            var revert = !result.Success;

            Transaction internalTransaction = this.transferProcessor.Process(
                this.stateRoot,
                result.ContractAddress,
                transactionContext,
                state.InternalTransfers,
                revert);

            (Money fee, List<TxOut> refundTxOuts) = this.refundProcessor.Process(
                callData,
                transactionContext.MempoolFee,
                transactionContext.Sender,
                result.GasConsumed,
                result.VmExecutionResult.ExecutionException);

            var executionResult = new SmartContractExecutionResult
            {
                To = !callData.IsCreateContract ? callData.ContractAddress : null,
                NewContractAddress = !revert && creation ? result.ContractAddress : null,
                Exception = result.VmExecutionResult.ExecutionException,
                GasConsumed = result.GasConsumed,
                Return = result.VmExecutionResult.Result,
                InternalTransaction = internalTransaction,
                Fee = fee,
                Refunds = refundTxOuts,
                Logs = state.LogHolder.GetRawLogs().ToLogs(this.contractPrimitiveSerializer)
            };

            return executionResult;
        }
    }
}
