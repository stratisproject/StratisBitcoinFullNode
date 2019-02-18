using System;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
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
        public VmExecutionResult Create(IStateRepository repository,
            ISmartContractState contractState,
            RuntimeObserver.IGasMeter gasMeter,
            byte[] contractCode,
            object[] parameters,
            string typeName = null)
        {
            string typeToInstantiate;
            ContractByteCode code;

            // Decompile the contract execution code
            Result<IContractModuleDefinition> moduleResult = this.moduleDefinitionReader.Read(contractCode);
            if (moduleResult.IsFailure)
            {
                this.logger.LogTrace(moduleResult.Error);
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
                    return VmExecutionResult.Fail(VmExecutionErrorKind.ValidationFailed, new SmartContractValidationException(validation.Errors).ToString());
                }

                typeToInstantiate = typeName ?? moduleDefinition.ContractType.Name;

                var observer = new Observer(gasMeter,  new MemoryMeter(MemoryUnitLimit));
                var rewriter = new ObserverRewriter(observer);

                if (!this.Rewrite(moduleDefinition, rewriter))
                {
                    return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Rewrite module failed");
                }

                Result<ContractByteCode> getCodeResult = this.GetByteCode(moduleDefinition);

                if (!getCodeResult.IsSuccess)
                {
                    return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Serialize module failed");
                }

                code = getCodeResult.Value;
            }

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeToInstantiate,
                contractState.Message.ContractAddress.ToUint160(),
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            this.LogExecutionContext(contract.State.Block, contract.State.Message, contract.Address);

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
        public VmExecutionResult ExecuteMethod(ISmartContractState contractState, RuntimeObserver.IGasMeter gasMeter, MethodCall methodCall, byte[] contractCode, string typeName)
        {
            ContractByteCode code;

            // Code we're loading from database - can assume it's valid.
            using (IContractModuleDefinition moduleDefinition = this.moduleDefinitionReader.Read(contractCode).Value)
            {
                var observer = new Observer(gasMeter, new MemoryMeter(MemoryUnitLimit));
                var rewriter = new ObserverRewriter(observer);

                if (!this.Rewrite(moduleDefinition, rewriter))
                {
                    return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Rewrite module failed");
                }

                Result<ContractByteCode> getCodeResult = this.GetByteCode(moduleDefinition);

                if (!getCodeResult.IsSuccess)
                {
                    return VmExecutionResult.Fail(VmExecutionErrorKind.RewriteFailed, "Serialize module failed");
                }

                code = getCodeResult.Value;
            }

            Result<IContract> contractLoadResult = this.Load(
                code,
                typeName,
                contractState.Message.ContractAddress.ToUint160(),
                contractState);

            if (!contractLoadResult.IsSuccess)
            {
                return VmExecutionResult.Fail(VmExecutionErrorKind.LoadFailed, contractLoadResult.Error);
            }

            IContract contract = contractLoadResult.Value;

            this.LogExecutionContext(contract.State.Block, contract.State.Message, contract.Address);

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
                this.logger.LogTrace(assemblyLoadResult.Error);

                return Result.Fail<IContract>(assemblyLoadResult.Error);
            }

            IContractAssembly contractAssembly = assemblyLoadResult.Value;

            Type type = contractAssembly.GetType(typeName);

            if (type == null)
            {
                const string typeNotFoundError = "Type not found!";

                this.logger.LogTrace(typeNotFoundError);

                return Result.Fail<IContract>(typeNotFoundError);
            }

            IContract contract;

            try
            {
                contract = Contract.CreateUninitialized(type, contractState, address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());

                return Result.Fail<IContract>("Exception occurred while instantiating contract instance");
            }

            return Result.Ok(contract);
        }

        private bool Rewrite(IContractModuleDefinition moduleDefinition, IILRewriter rewriter)
        {
            try
            {
                moduleDefinition.Rewrite(rewriter);
                return true;
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[CONTRACT_REWRITE_FAILED]");
            }

            return false;
        }

        private Result<ContractByteCode> GetByteCode(IContractModuleDefinition moduleDefinition)
        {
            try
            {
                ContractByteCode code = moduleDefinition.ToByteCode();

                return Result.Ok(code);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[CONTRACT_TOBYTECODE_FAILED]");

                return Result.Fail<ContractByteCode>("Exception occurred while serializing module");
            }
        }

        internal void LogExecutionContext(IBlock block, IMessage message, uint160 contractAddress)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress()));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));
            this.logger.LogTrace("{0}", builder.ToString());
        }
    }
}