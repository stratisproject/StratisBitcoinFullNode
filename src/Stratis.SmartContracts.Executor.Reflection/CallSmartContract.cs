using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public sealed class CallSmartContract : ISmartContractExecutor
    {
        private readonly ILogger logger;
        private readonly IContractStateRepository stateSnapshot;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ICallDataSerializer serializer;

        public CallSmartContract(IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            ICallDataSerializer serializer,
            IContractStateRepository stateSnapshot,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.loggerFactory = loggerFactory;
            this.stateSnapshot = stateSnapshot;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.serializer = serializer;
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            var callDataDeserializationResult = this.serializer.Deserialize(transactionContext.ScriptPubKey.ToBytes());

            // TODO Handle deserialization failure

            var callData = callDataDeserializationResult.Value;

            // Get the contract code (dll) from the repository.
            byte[] contractExecutionCode = this.stateSnapshot.GetCode(callData.ContractAddress);
            if (contractExecutionCode == null)
            {
                return SmartContractExecutionResult.ContractDoesNotExist(callData.MethodName);
            }

            // Execute the call to the contract.
            return this.CreateContextAndExecute(callData.ContractAddress, contractExecutionCode, callData.MethodName, transactionContext, callData);
        }

        private ISmartContractExecutionResult CreateContextAndExecute(uint160 contractAddress, byte[] contractCode,
            string methodName, ISmartContractTransactionContext transactionContext,
            CallData callData)
        {
            this.logger.LogTrace("()");

            var block = new Block(transactionContext.BlockHeight,
                transactionContext.CoinbaseAddress.ToAddress(this.network));

            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    contractAddress.ToAddress(this.network),
                    transactionContext.Sender.ToAddress(this.network),
                    transactionContext.TxOutValue,
                    callData.GasLimit
                ),
                contractAddress,
                callData.GasPrice,
                callData.MethodParameters
            );

            LogExecutionContext(this.logger, block, executionContext.Message, contractAddress, callData);

            var gasMeter = new GasMeter(callData.GasLimit);

            IPersistenceStrategy persistenceStrategy =
                new MeteredPersistenceStrategy(this.stateSnapshot, gasMeter, this.keyEncodingStrategy);

            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            var result = this.vm.ExecuteMethod(
                contractCode,
                methodName,
                executionContext,
                gasMeter,
                persistentState,
                this.stateSnapshot);

            var revert = result.ExecutionException != null;

            this.logger.LogTrace("(-)");

            var internalTransaction = this.transferProcessor.Process(this.stateSnapshot, 
                callData, 
                transactionContext, 
                result.InternalTransfers,
                revert);

            (var fee, var refundTxOuts) = this.refundProcessor.Process(
                callData, 
                transactionContext.MempoolFee, 
                transactionContext.Sender,
                result.GasConsumed,
                result.ExecutionException);

            var executionResult = new SmartContractExecutionResult
            {
                Exception = result.ExecutionException,
                GasConsumed = result.GasConsumed,
                Return = result.Result,
                InternalTransaction = internalTransaction,
                Fee = fee,
                Refunds = refundTxOuts
            };                       

            if (revert)
            {
                this.stateSnapshot.Rollback();
            }
            else
            {
                this.stateSnapshot.Commit();
            }

            return executionResult;
        }

        internal void LogExecutionContext(ILogger logger, IBlock block, IMessage message, uint160 contractAddress,
            CallData callData)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress(this.network)));
            builder.Append(string.Format("{0}:{1},", nameof(callData.GasPrice), callData.GasPrice));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.GasLimit), message.GasLimit, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));

            if (callData.MethodParameters != null && callData.MethodParameters.Length > 0)
                builder.Append(string.Format(",{0}:{1}", nameof(callData.MethodParameters), callData.MethodParameters));

            logger.LogTrace("{0}", builder.ToString());
        }
    }
}