using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public sealed class CreateSmartContract : ISmartContractExecutor
    {
        private readonly ILogger logger;
        private readonly IContractStateRepository stateSnapshot;
        private readonly SmartContractValidator validator;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;

        public CreateSmartContract(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            IContractStateRepository stateSnapshot,
            SmartContractValidator validator,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.loggerFactory = loggerFactory;
            this.stateSnapshot = stateSnapshot;
            this.validator = validator;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            var carrier = SmartContractCarrier.Deserialize(transactionContext);

            // Create a new address for the contract.
            uint160 newContractAddress = carrier.GetNewContractAddress();

            // Create an account for the contract in the state repository.
            this.stateSnapshot.CreateAccount(newContractAddress);

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(carrier.ContractExecutionCode);
            SmartContractValidationResult validation = this.validator.Validate(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                return SmartContractExecutionResult.ValidationFailed(carrier, validation);
            }

            var block = new Block(transactionContext.BlockHeight, transactionContext.CoinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    newContractAddress.ToAddress(this.network),
                    carrier.Sender.ToAddress(this.network),
                    carrier.Value,
                    carrier.GasLimit
                ),
                newContractAddress,
                carrier.GasPrice,
                carrier.MethodParameters
            );

            LogExecutionContext(this.logger, block, executionContext.Message, newContractAddress, carrier);

            var gasMeter = new GasMeter(carrier.GasLimit);

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, newContractAddress, this.network);

            // TODO push TXExecutorFactory to DI
            var vm = new ReflectionVirtualMachine(new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network), this.loggerFactory, persistentState, this.stateSnapshot);

            // Push internal tx executor and getbalance down into VM
            ISmartContractExecutionResult result = vm.Create(carrier.ContractExecutionCode, executionContext, gasMeter);

            if (result.Revert)
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_FAILED]");
                return result;
            }

            result.NewContractAddress = newContractAddress;

            this.stateSnapshot.SetCode(newContractAddress, carrier.ContractExecutionCode);

            this.logger.LogTrace("(-):{0}={1}", nameof(newContractAddress), newContractAddress);

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
            builder.Append(string.Format("{0}:{1},", nameof(carrier.GasPrice), carrier.GasPrice));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.GasLimit), message.GasLimit, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));

            if (carrier.MethodParameters != null && carrier.MethodParameters.Length > 0)
                builder.Append(string.Format(",{0}:{1}", nameof(carrier.MethodParameters), carrier.MethodParameters));

            logger.LogTrace("{0}", builder.ToString());
        }
    }
}