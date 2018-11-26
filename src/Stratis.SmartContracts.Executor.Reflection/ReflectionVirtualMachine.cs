﻿using System;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using RuntimeObserver;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.ILRewrite;
using Stratis.SmartContracts.Executor.Reflection.Loader;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : IVirtualMachine
    {
        private readonly ILogger logger;
        private readonly ISmartContractValidator validator;
        private readonly ILoader assemblyLoader;
        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        public const int VmVersion = 1;
        public const long MemoryUnitLimit = 100_000;

        public ReflectionVirtualMachine(ISmartContractValidator validator,
            ILoggerFactory loggerFactory,
            ILoader assemblyLoader,
            IContractModuleDefinitionReader moduleDefinitionReader)
        {
            this.validator = validator;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.assemblyLoader = assemblyLoader;
            this.moduleDefinitionReader = moduleDefinitionReader;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public VmExecutionResult Create(IStateRepository repository, ISmartContractState contractState, byte[] contractCode, object[] parameters, string typeName = null)
        {
            string typeToInstantiate;
            ContractByteCode code;

            // Decompile the contract execution code
            Result<IContractModuleDefinition> moduleResult = this.moduleDefinitionReader.Read(contractCode);
            if (moduleResult.IsFailure)
            {
                this.logger.LogTrace("(-)[CONTRACT_BYTECODE_INVALID]");
                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, "Contract bytecode is not valid IL.");
            }

            // Validate contract execution code
            using (IContractModuleDefinition moduleDefinition = moduleResult.Value)
            {
                SmartContractValidationResult validation = moduleDefinition.Validate(this.validator);

                // If validation failed, refund the sender any remaining gas.
                if (!validation.IsValid)
                {
                    this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                    // TODO: List errors by string.
                    return VmExecutionResult.Fail(VmExecutionErrorKind.ValidationFailed, new SmartContractValidationException(validation.Errors).ToString());
                }

                typeToInstantiate = typeName ?? moduleDefinition.ContractType.Name;

                var observer = new Observer(contractState.GasMeter, MemoryUnitLimit);
                var rewriter = new ObserverRewriter(observer);
                moduleDefinition.Rewrite(rewriter);

                code = moduleDefinition.ToByteCode();
            }

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeToInstantiate,
                contractState.Message.ContractAddress.ToUint160(),
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                LogErrorMessage(contractLoadResult.Error);

                this.logger.LogTrace("(-)[LOAD_CONTRACT_FAILED]");

                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            LogExecutionContext(this.logger, contract.State.Block, contract.State.Message, contract.Address);

            // Set the code and the Type before the method is invoked
            repository.SetCode(contract.Address, contractCode);
            repository.SetContractType(contract.Address, typeToInstantiate);

            // Invoke the constructor of the provided contract code
            IContractInvocationResult invocationResult = contract.InvokeConstructor(parameters);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_FAILED]");
                return GetInvocationVmErrorResult(invocationResult);
            }

            this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_SUCCEEDED]");

            return VmExecutionResult.Ok(invocationResult.Return, typeToInstantiate);
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public VmExecutionResult ExecuteMethod(ISmartContractState contractState, MethodCall methodCall, byte[] contractCode, string typeName)
        {
            ContractByteCode code;

            // Code we're loading from database - can assume it's valid.
            using (IContractModuleDefinition moduleDefinition = this.moduleDefinitionReader.Read(contractCode).Value)
            {
                var observer = new Observer(contractState.GasMeter, MemoryUnitLimit);
                var rewriter = new ObserverRewriter(observer);
                moduleDefinition.Rewrite(rewriter);
                code = moduleDefinition.ToByteCode();
            }

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeName,
                contractState.Message.ContractAddress.ToUint160(),
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                LogErrorMessage(contractLoadResult.Error);

                this.logger.LogTrace("(-)[LOAD_CONTRACT_FAILED]");

                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            LogExecutionContext(this.logger, contract.State.Block, contract.State.Message, contract.Address);

            IContractInvocationResult invocationResult = contract.Invoke(methodCall);

            if (!invocationResult.IsSuccess)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]");

                return GetInvocationVmErrorResult(invocationResult);
            }

            this.logger.LogTrace("[CALL_CONTRACT_INSTANTIATION_SUCCEEDED]");

            return VmExecutionResult.Ok(invocationResult.Return, typeName);
        }

        private static VmExecutionResult GetInvocationVmErrorResult(IContractInvocationResult invocationResult)
        {
            if (invocationResult.InvocationErrorType == ContractInvocationErrorType.OutOfGas)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.OutOfGas, invocationResult.ErrorMessage);
            }

            if (invocationResult.InvocationErrorType == ContractInvocationErrorType.OverMemoryLimit)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.OutOfResources, invocationResult.ErrorMessage);
            }

            return VmExecutionResult.Fail(VmExecutionErrorKind.InvocationFailed, invocationResult.ErrorMessage);
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

        private void LogErrorMessage(string error)
        {
            this.logger.LogTrace("{0}", error);
        }

        internal void LogExecutionContext(ILogger logger, IBlock block, IMessage message, uint160 contractAddress)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress()));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));
            logger.LogTrace("{0}", builder.ToString());
        }
    }
}