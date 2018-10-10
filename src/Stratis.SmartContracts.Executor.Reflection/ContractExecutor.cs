using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.ResultProcessors;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Deserializes raw contract transaction data, creates an external create/call message, and applies the message to the state.
    /// </summary>
    public class ContractExecutor : IContractExecutor
    {
        private readonly ILogger logger;
        private readonly IStateRepository stateRoot;
        private readonly IContractRefundProcessor refundProcessor;
        private readonly IContractTransferProcessor transferProcessor;
        private readonly ICallDataSerializer serializer;
        private readonly Network network;
        private readonly IStateFactory stateFactory;
        private readonly IStateProcessor stateProcessor;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;

        public ContractExecutor(ILoggerFactory loggerFactory,
            ICallDataSerializer serializer,
            IStateRepository stateRoot,
            IContractRefundProcessor refundProcessor,
            IContractTransferProcessor transferProcessor,
            Network network,
            IStateFactory stateFactory,
            IStateProcessor stateProcessor,
            IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.stateRoot = stateRoot;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.serializer = serializer;
            this.network = network;
            this.stateFactory = stateFactory;
            this.stateProcessor = stateProcessor;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
        }

        public IContractExecutionResult Execute(IContractTransactionContext transactionContext)
        {
            // Deserialization can't fail because this has already been through SmartContractFormatRule.
            Result<ContractTxData> callDataDeserializationResult = this.serializer.Deserialize(transactionContext.Data);
            ContractTxData callData = callDataDeserializationResult.Value;

            bool creation = callData.IsCreateContract;

            var block = new Block(
                transactionContext.BlockHeight,
                transactionContext.CoinbaseAddress.ToAddress(this.network)
            );

            IState state = this.stateFactory.Create(
                this.stateRoot,
                block,
                transactionContext.TxOutValue,
                transactionContext.TransactionHash);

            StateTransitionResult result;
            IState newState = state.Snapshot();

            if (creation)
            {
                var message = new ExternalCreateMessage(
                    transactionContext.Sender,
                    transactionContext.TxOutValue,
                    callData.GasLimit,
                    callData.ContractExecutionCode,
                    callData.MethodParameters
                );

                result = this.stateProcessor.Apply(newState, message);
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

                result = this.stateProcessor.Apply(newState, message);
            }

            bool revert = !result.IsSuccess;

            Transaction internalTransaction = this.transferProcessor.Process(
                newState.ContractState,
                result.Success?.ContractAddress,
                transactionContext,
                newState.InternalTransfers,
                revert);

            if (result.IsSuccess)
                state.TransitionTo(newState);

            bool outOfGas = result.IsFailure && result.Error.Kind == StateTransitionErrorKind.OutOfGas;

            (Money fee, TxOut refundTxOut) = this.refundProcessor.Process(
                callData,
                transactionContext.MempoolFee,
                transactionContext.Sender,
                result.GasConsumed,
                outOfGas);

            var executionResult = new SmartContractExecutionResult
            {
                To = !callData.IsCreateContract ? callData.ContractAddress : null,
                NewContractAddress = !revert && creation ? result.Success?.ContractAddress : null,
                ErrorMessage = result.Error?.GetErrorMessage(),
                Revert = revert,
                GasConsumed = result.GasConsumed,
                Return = result.Success?.ExecutionResult,
                InternalTransaction = internalTransaction,
                Fee = fee,
                Refund = refundTxOut,
                Logs = state.GetLogs(this.contractPrimitiveSerializer)
            };

            return executionResult;
        }
    }
}
