﻿using System;
using System.Collections.Generic;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.Loader;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly ISmartContractValidator validator;
        private readonly IAddressGenerator addressGenerator;
        private readonly ILoader assemblyLoader;
        public static int VmVersion = 1;

        public ReflectionVirtualMachine(ISmartContractValidator validator,
            InternalTransactionExecutorFactory internalTransactionExecutorFactory,
            ILoggerFactory loggerFactory,
            Network network,
            IAddressGenerator addressGenerator,
            ILoader assemblyLoader)
        {
            this.validator = validator;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
            this.addressGenerator = addressGenerator;
            this.assemblyLoader = assemblyLoader;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public VmExecutionResult Create(IGasMeter gasMeter,
            IContractStateRepository repository,
            ICreateData createData,
            ITransactionContext transactionContext,
            string typeName = null)
        {
            this.logger.LogTrace("()");

            // TODO: Spend Validation + Creation Fee here.

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(createData.ContractExecutionCode);

            SmartContractValidationResult validation = decompilation.Validate(this.validator);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, new SmartContractValidationException(validation.Errors));
            }

            string typeToInstantiate = typeName ?? decompilation.ContractType.Name;

            decompilation.InjectConstructorGas();

            var internalTransferList = new List<TransferInfo>();

            uint160 address = this.addressGenerator.GenerateAddress(transactionContext.TransactionHash, transactionContext.GetNonceAndIncrement());

            ISmartContractState contractState = this.SetupState(internalTransferList, gasMeter, repository, transactionContext, address);

            Result<IContract> contractLoadResult = this.Load(
                decompilation.ToByteCode(),
                typeToInstantiate,
                address,
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                // TODO this is temporary until we improve error handling overloads
                var exception = new Exception(contractLoadResult.Error);

                LogException(exception);

                this.logger.LogTrace("(-)[LOAD_CONTRACT_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

                return VmExecutionResult.Error(gasMeter.GasConsumed, exception);
            }

            IContract contract = contractLoadResult.Value;

            LogExecutionContext(this.logger, contract.State.Block, contract.State.Message, contract.Address, createData);

            // Create an account for the contract in the state repository.
            repository.CreateAccount(contract.Address);

            // Invoke the constructor of the provided contract code
            IContractInvocationResult invocationResult = contract.InvokeConstructor(createData.MethodParameters);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_FAILED]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, new Exception("Constructor invocation failed!"));
            }

            this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_SUCCEEDED]");
            
            this.logger.LogTrace("(-):{0}={1}, {2}={3}", nameof(contract.Address), contract.Address, nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            repository.SetCode(contract.Address, createData.ContractExecutionCode);
            repository.SetContractType(contract.Address, contract.Type.Name);

            return VmExecutionResult.CreationSuccess(contract.Address, internalTransferList, gasMeter.GasConsumed, invocationResult.Return);
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public VmExecutionResult ExecuteMethod(
            IGasMeter gasMeter,
            IContractStateRepository repository,
            ICallData callData,
            ITransactionContext transactionContext)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(callData.MethodName), callData.MethodName);

            if (callData.MethodName == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_METHODNAME_NOT_GIVEN]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, null);
            }

            byte[] contractExecutionCode = repository.GetCode(callData.ContractAddress);

            string typeName = repository.GetContractType(callData.ContractAddress);

            if (contractExecutionCode == null)
            {
                return VmExecutionResult.Error(gasMeter.GasConsumed, new SmartContractDoesNotExistException(callData.MethodName));
            }

            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(contractExecutionCode);

            decompilation.InjectMethodGas(typeName, callData.MethodName);

            var internalTransferList = new List<TransferInfo>();

            ISmartContractState contractState = this.SetupState(internalTransferList, gasMeter, repository, transactionContext, callData.ContractAddress);

            Result<IContract> contractLoadResult = this.Load(
                decompilation.ToByteCode(),
                typeName,
                callData.ContractAddress,
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                // TODO this is temporary until we improve error handling overloads
                var exception = new Exception(contractLoadResult.Error);

                LogException(exception);

                this.logger.LogTrace("(-)[LOAD_CONTRACT_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

                return VmExecutionResult.Error(gasMeter.GasConsumed, exception);
            }

            IContract contract = contractLoadResult.Value;

            LogExecutionContext(this.logger, contract.State.Block, contract.State.Message, contract.Address, callData);

            IContractInvocationResult invocationResult = contract.Invoke(callData.MethodName, callData.MethodParameters);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);
                return VmExecutionResult.Error(gasMeter.GasConsumed, new Exception("Method invocation failed!"));
            }

            this.logger.LogTrace("[CALL_CONTRACT_INSTANTIATION_SUCCEEDED]");

            this.logger.LogTrace("(-):{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            return VmExecutionResult.Success(internalTransferList, gasMeter.GasConsumed, invocationResult.Return);
        }

        /// <summary>
        /// Sets up the state object for the contract execution
        /// </summary>
        private ISmartContractState SetupState(
            List<TransferInfo> internalTransferList,
            IGasMeter gasMeter,
            IContractStateRepository repository,
            ITransactionContext transactionContext,
            uint160 contractAddress)
        {
            IPersistenceStrategy persistenceStrategy =
                new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());

            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            IInternalTransactionExecutor internalTransactionExecutor = this.internalTransactionExecutorFactory.Create(this, repository, internalTransferList, transactionContext);

            var balanceState = new BalanceState(repository, transactionContext.Amount, internalTransferList);

            var contractState = new SmartContractState(
                new Block(
                    transactionContext.BlockHeight,
                    transactionContext.Coinbase.ToAddress(this.network)
                ),
                new Message(
                    contractAddress.ToAddress(this.network),
                    transactionContext.From.ToAddress(this.network),
                    transactionContext.Amount
                ),
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(contractAddress));
            return contractState;
        }

        /// <summary>
        /// Loads the contract bytecode and returns an <see cref="IContract"/> representing an uninitialized contract instance.
        /// </summary>
        private Result<IContract> Load(
            ContractByteCode byteCode,
            string typeName, 
            uint160 address,
            ISmartContractState contractState)
        {
            Result<IContractAssembly> assemblyLoadResult = this.assemblyLoader.Load(byteCode);

            if (!assemblyLoadResult.IsSuccess)
            {
                return Result.Fail<IContract>(assemblyLoadResult.Error);
            }

            IContractAssembly contractAssembly = assemblyLoadResult.Value;

            Type type = contractAssembly.GetType(typeName);

            if (type == null)
            {
                return Result.Fail<IContract>("Type not found!");
            }

            IContract contract = Contract.CreateUninitialized(type, contractState, address);

            return Result.Ok(contract);
        }

        private void LogException(Exception exception)
        {
            this.logger.LogTrace("{0}", exception.Message);
            if (exception.InnerException != null)
                this.logger.LogTrace("{0}", exception.InnerException.Message);
        }

        internal void LogExecutionContext(ILogger logger, IBlock block, IMessage message, uint160 contractAddress, IBaseContractTransactionData callData)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress(this.network)));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));

            if (callData.MethodParameters != null && callData.MethodParameters.Length > 0)
                builder.Append(string.Format(",{0}:{1}", nameof(callData.MethodParameters), callData.MethodParameters));

            logger.LogTrace("{0}", builder.ToString());
        }
    }
}