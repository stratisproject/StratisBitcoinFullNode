using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Executes the smart contract code, be it a OP_CREATECONTRACT or OP_CALLCONTRACT
    /// </summary>
    public abstract class SmartContractExecutor : ISmartContractExecutor
    {
        protected readonly ISmartContractTransactionContext transactionContext;
        protected readonly SmartContractCarrier carrier;
        protected readonly IGasMeter gasMeter;
        protected readonly Network network;
        protected readonly IContractStateRepository stateSnapshot;
        protected readonly SmartContractValidator validator;
        protected readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILogger logger;
        protected readonly ILoggerFactory loggerFactory;

        internal ISmartContractExecutionResult Result { get; set; }


        protected SmartContractExecutor(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext,
            SmartContractValidator validator)
        {
            this.carrier = SmartContractCarrier.Deserialize(transactionContext);
            this.gasMeter = new GasMeter(this.carrier.GasLimit);
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
            this.stateSnapshot = stateSnapshot.StartTracking();
            this.transactionContext = transactionContext;
            this.validator = validator;
        }

        public ISmartContractExecutionResult Execute()
        {
            try
            {
                PreExecute();
                OnExecute();
                PostExecute();
            }
            catch (Exception unhandled)
            {
                this.logger.LogError("An unhandled exception occurred {0}", unhandled.Message);
                if (unhandled.InnerException != null)
                    this.logger.LogError("{0}", unhandled.InnerException.Message);
            }

            return this.Result;
        }

        /// <summary>
        /// Any logic that should happen before we start contract execution and/or validation
        /// happens here.
        /// <para>
        /// A base fee should be spent before contract execution starts.
        /// </para>
        /// </summary>
        private void PreExecute()
        {
            this.gasMeter.Spend((Gas)GasPriceList.BaseCost);
        }

        public abstract void OnExecute();

        /// <summary>
        /// Any logic that should happen after contract execution has taken place happens here.
        /// </summary>
        private void PostExecute()
        {
            new SmartContractExecutorResultProcessor(this.Result, this.loggerFactory).Process(this.carrier, this.transactionContext.MempoolFee);
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

    public sealed class CreateSmartContract : SmartContractExecutor
    {
        private readonly ILogger logger;

        public CreateSmartContract(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext,
            SmartContractValidator validator)
            : base(keyEncodingStrategy, loggerFactory, network, stateSnapshot, transactionContext, validator)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public override void OnExecute()
        {
            this.logger.LogTrace("()");

            // Create a new address for the contract.
            uint160 newContractAddress = this.carrier.GetNewContractAddress();

            // Create an account for the contract in the state repository.
            this.stateSnapshot.CreateAccount(newContractAddress);

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(this.carrier.ContractExecutionCode);
            SmartContractValidationResult validation = this.validator.Validate(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.Result = SmartContractExecutionResult.ValidationFailed(this.carrier, validation);
                this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                return;
            }

            var block = new Block(this.transactionContext.BlockHeight, this.transactionContext.CoinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    newContractAddress.ToAddress(this.network),
                    this.carrier.Sender.ToAddress(this.network),
                    this.carrier.Value,
                    this.carrier.GasLimit
                ),
                newContractAddress,
                this.carrier.GasPrice,
                this.carrier.MethodParameters
            );

            LogExecutionContext(this.logger, block, executionContext.Message, newContractAddress, this.carrier);

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, this.gasMeter, new BasicKeyEncodingStrategy());
            var persistentState = new PersistentState(persistenceStrategy, newContractAddress, this.network);

            // TODO push TXExecutorFactory to DI
            var vm = new ReflectionVirtualMachine(new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network), this.loggerFactory, persistentState, this.stateSnapshot);

            // Push internal tx executor and getbalance down into VM
            this.Result = vm.Create(this.carrier.ContractExecutionCode, executionContext, this.gasMeter);

            if (this.Result.Revert)
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_FAILED]");
                return;
            }

            this.Result.NewContractAddress = newContractAddress;

            // To start with, no value transfers on create. Can call other contracts but send 0 only.
            this.stateSnapshot.SetCode(newContractAddress, this.carrier.ContractExecutionCode);
            this.stateSnapshot.Commit();

            this.logger.LogTrace("(-):{0}={1}", nameof(newContractAddress), newContractAddress);
        }
    }

    public sealed class CallSmartContract : SmartContractExecutor
    {
        private readonly ILogger logger;

        public CallSmartContract(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext,
            SmartContractValidator validator)
            : base(keyEncodingStrategy, loggerFactory, network, stateSnapshot, transactionContext, validator)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public override void OnExecute()
        {
            this.logger.LogTrace("()");

            // Get the contract code (dll) from the repository.
            byte[] contractExecutionCode = this.stateSnapshot.GetCode(this.carrier.ContractAddress);
            if (contractExecutionCode == null)
            {
                this.Result = SmartContractExecutionResult.ContractDoesNotExist(this.carrier);
                return;
            }

            // Execute the call to the contract.
            this.Result = this.CreateContextAndExecute(this.carrier.ContractAddress, contractExecutionCode, this.carrier.MethodName);

            if (this.Result.Revert)
            {
                this.logger.LogTrace("(-)[CALL_CONTRACT_FAILED]:{0}={1}", nameof(this.carrier.ContractAddress), this.carrier.ContractAddress);
                this.RevertExecution();
            }
            else
            {
                this.logger.LogTrace("(-)[CALL_CONTRACT_SUCCEEDED]:{0}={1}", nameof(this.carrier.ContractAddress), this.carrier.ContractAddress);
                this.CommitExecution(this.Result.InternalTransfers);
            }
        }

        private ISmartContractExecutionResult CreateContextAndExecute(uint160 contractAddress, byte[] contractCode, string methodName)
        {
            this.logger.LogTrace("()");

            var block = new Block(this.transactionContext.BlockHeight, this.transactionContext.CoinbaseAddress.ToAddress(this.network));
            var executionContext = new SmartContractExecutionContext
            (
                block,
                new Message(
                    contractAddress.ToAddress(this.network),
                    this.carrier.Sender.ToAddress(this.network),
                    this.carrier.Value,
                    this.carrier.GasLimit
                ),
                contractAddress,
                this.carrier.GasPrice,
                this.carrier.MethodParameters
            );

            LogExecutionContext(this.logger, block, executionContext.Message, contractAddress, this.carrier);

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(this.stateSnapshot, this.gasMeter, this.keyEncodingStrategy);
            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            var vm = new ReflectionVirtualMachine(new InternalTransactionExecutorFactory(this.keyEncodingStrategy, this.loggerFactory, this.network), this.loggerFactory, persistentState, this.stateSnapshot);
            ISmartContractExecutionResult result = vm.ExecuteMethod(
                contractCode,
                methodName,
                executionContext,
                this.gasMeter);

            this.logger.LogTrace("(-)");

            return result;
        }

        /// <summary>
        /// Contract execution completed successfully, commit state.
        /// <para>
        /// We need to append a condensing transaction to the block if funds are moved.
        /// </para>
        /// </summary>
        /// <param name="transfers"></param>
        private void CommitExecution(IList<TransferInfo> transfers)
        {
            this.logger.LogTrace("()");

            if (transfers != null && transfers.Any() || this.carrier.Value > 0)
            {
                this.logger.LogTrace("[CREATE_CONDENSING_TX]:{0}={1},{2}={3}", nameof(transfers), transfers.Count, nameof(this.carrier.Value), this.carrier.Value);
                var condensingTx = new CondensingTx(this.carrier.ContractAddress, this.loggerFactory, transfers, this.stateSnapshot, this.network, this.transactionContext);
                this.Result.InternalTransaction = condensingTx.CreateCondensingTransaction();
            }

            this.stateSnapshot.Commit();

            this.logger.LogTrace("(-)");

        }

        /// <summary>
        /// If funds were sent to the contract and execution failed, we need to send it back to the sender.
        /// </summary>
        private void RevertExecution()
        {
            this.logger.LogTrace("()");

            if (this.carrier.Value > 0)
            {
                this.logger.LogTrace("[CREATE_REFUND_TX]:{0}={1}", nameof(this.carrier.Value), this.carrier.Value);
                Transaction tx = new CondensingTx(this.carrier.ContractAddress, this.loggerFactory, this.network, this.transactionContext).CreateRefundTransaction();
                this.Result.InternalTransaction = tx;
            }

            this.logger.LogTrace("(-)");
        }
    }
}