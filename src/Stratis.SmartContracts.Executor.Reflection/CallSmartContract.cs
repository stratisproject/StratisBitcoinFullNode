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

        public CallSmartContract(IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
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
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            var carrier = SmartContractCarrier.Deserialize(transactionContext);

            // Get the contract code (dll) from the repository.
            byte[] contractExecutionCode = this.stateSnapshot.GetCode(carrier.CallData.ContractAddress);
            if (contractExecutionCode == null)
            {
                return SmartContractExecutionResult.ContractDoesNotExist(carrier);
            }

            // Execute the call to the contract.
            return this.CreateContextAndExecute(carrier.CallData.ContractAddress, contractExecutionCode, carrier.CallData.MethodName, transactionContext, carrier);
        }

        private ISmartContractExecutionResult CreateContextAndExecute(uint160 contractAddress, byte[] contractCode,
            string methodName, ISmartContractTransactionContext transactionContext, SmartContractCarrier carrier)
        {
            this.logger.LogTrace("()");

            var block = new Block(transactionContext.BlockHeight, transactionContext.CoinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    contractAddress.ToAddress(this.network),
                    carrier.Sender.ToAddress(this.network),
                    carrier.Value,
                    carrier.CallData.GasLimit
                ),
                contractAddress,
                carrier.CallData.GasPrice,
                carrier.MethodParameters
            );

            LogExecutionContext(this.logger, block, executionContext.Message, contractAddress, carrier);

            var gasMeter = new GasMeter(carrier.CallData.GasLimit);

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            ISmartContractExecutionResult result = this.vm.ExecuteMethod(
                contractCode,
                methodName,
                executionContext,
                gasMeter,
                persistentState, 
                this.stateSnapshot);

            this.logger.LogTrace("(-)");

            // Post-execute
            this.transferProcessor.Process(carrier, result, this.stateSnapshot, transactionContext);
            this.refundProcessor.Process(result, carrier, transactionContext.MempoolFee);

            if (result.Revert)
            {
                this.stateSnapshot.Rollback();
            }
            else
            {
                this.stateSnapshot.Commit();
            }

            return result;
        }

        internal void LogExecutionContext(ILogger logger, IBlock block, IMessage message, uint160 contractAddress, SmartContractCarrier carrier)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress(this.network)));
            builder.Append(string.Format("{0}:{1},", nameof(carrier.CallData.GasPrice), carrier.CallData.GasPrice));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.GasLimit), message.GasLimit, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));

            if (carrier.MethodParameters != null && carrier.MethodParameters.Length > 0)
                builder.Append(string.Format(",{0}:{1}", nameof(carrier.MethodParameters), carrier.MethodParameters));

            logger.LogTrace("{0}", builder.ToString());
        }
    }
}