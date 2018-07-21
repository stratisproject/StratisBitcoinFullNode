using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.Lifecycle;
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
        public static int VmVersion = 1;

        public ReflectionVirtualMachine(ISmartContractValidator validator,
            InternalTransactionExecutorFactory internalTransactionExecutorFactory,
            ILoggerFactory loggerFactory,
            Network network)
        {
            this.validator = validator;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public VmExecutionResult Create(IGasMeter gasMeter,
            IContractStateRepository repository,
            ICreateData createData,
            ITransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(createData.ContractExecutionCode);

            SmartContractValidationResult validation = this.validator.Validate(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, new SmartContractValidationException(validation.Errors));
            }

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToConstructor(createData.ContractExecutionCode, decompilation.ContractType.Name);

            Type contractType = Load(gasInjectedCode, decompilation.ContractType.Name);
            
            uint160 contractAddress = Core.NewContractAddressExtension.GetContractAddressFromTransactionHash(transactionContext.TransactionHash);

            // Create an account for the contract in the state repository.
            repository.CreateAccount(contractAddress);
            
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());

            var persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            var internalTransferList = new List<TransferInfo>();

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
                    transactionContext.Amount,
                    createData.GasLimit
                ),
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(contractAddress));

            LogExecutionContext(this.logger, contractState.Block, contractState.Message, contractAddress, createData);

            // Invoke the constructor of the provided contract code
            LifecycleResult result = SmartContractConstructor.Construct(contractType, contractState, createData.MethodParameters);

            if (!result.Success)
            {
                LogException(result.Exception);

                this.logger.LogTrace("(-)[CREATE_CONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

                return VmExecutionResult.Error(gasMeter.GasConsumed, result.Exception.InnerException ?? result.Exception);
            }

            this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_SUCCEEDED]");
            
            this.logger.LogTrace("(-):{0}={1}, {2}={3}", nameof(contractAddress), contractAddress, nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            repository.SetCode(contractAddress, createData.ContractExecutionCode);
            repository.SetContractType(contractAddress, contractType.Name);

            return VmExecutionResult.CreationSuccess(contractAddress, internalTransferList, gasMeter.GasConsumed, result.Object);
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

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

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

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToContractMethod(contractExecutionCode, typeName, callData.MethodName);
            
            Type contractType = Load(gasInjectedCode, typeName);

            if (contractType == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_CONTRACTTYPE_NULL]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, null);
            }

            uint160 contractAddress = callData.ContractAddress;

            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());

            IPersistentState persistentState = new PersistentState(persistenceStrategy, contractAddress, this.network);

            var internalTransferList = new List<TransferInfo>();

            IInternalTransactionExecutor internalTransactionExecutor = this.internalTransactionExecutorFactory.Create(this, repository, internalTransferList, transactionContext);

            var balanceState = new BalanceState(repository, transactionContext.Amount, internalTransferList);

            var contractState = new SmartContractState(
                new Block(
                    transactionContext.BlockHeight,
                    transactionContext.Coinbase.ToAddress(this.network)
                ),
                new Message(
                    callData.ContractAddress.ToAddress(this.network),
                    transactionContext.From.ToAddress(this.network),
                    transactionContext.Amount,
                    callData.GasLimit
                ),
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(callData.ContractAddress));

            LogExecutionContext(this.logger, contractState.Block, contractState.Message, contractAddress, callData);

            LifecycleResult result = SmartContractRestorer.Restore(contractType, contractState);

            if (!result.Success)
            {
                LogException(result.Exception);

                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);
               
                return VmExecutionResult.Error(gasMeter.GasConsumed, result.Exception.InnerException ?? result.Exception);
            }
            else
                this.logger.LogTrace("[CALL_CONTRACT_INSTANTIATION_SUCCEEDED]");

            object methodResult = null;

            try
            {
                MethodInfo methodToInvoke = contractType.GetMethod(callData.MethodName);
                if (methodToInvoke == null)
                    throw new ArgumentException(string.Format("[CALLCONTRACT_METHODTOINVOKE_NULL_DOESNOT_EXIST]:{0}={1}", nameof(callData.MethodName), callData.MethodName));

                if (methodToInvoke.IsConstructor)
                    throw new ConstructorInvocationException("[CALLCONTRACT_CANNOT_INVOKE_CTOR]");

                SmartContract smartContract = result.Object;
                methodResult = methodToInvoke.Invoke(smartContract, callData.MethodParameters);
            }
            catch (ArgumentException argumentException)
            {
                LogException(argumentException);
                return VmExecutionResult.Error(gasMeter.GasConsumed, argumentException);
            }
            catch (TargetInvocationException targetException)
            {
                LogException(targetException);
                return VmExecutionResult.Error(gasMeter.GasConsumed, targetException.InnerException ?? targetException);
            }
            catch (TargetParameterCountException parameterException)
            {
                LogException(parameterException);
            }
            catch (ConstructorInvocationException constructorInvocationException)
            {
                LogException(constructorInvocationException);
                return VmExecutionResult.Error(gasMeter.GasConsumed, constructorInvocationException);
            }

            this.logger.LogTrace("(-):{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            return VmExecutionResult.Success(internalTransferList, gasMeter.GasConsumed, methodResult);
        }

        private void LogException(Exception exception)
        {
            this.logger.LogTrace("{0}", exception.Message);
            if (exception.InnerException != null)
                this.logger.LogTrace("{0}", exception.InnerException.Message);
        }

        /// <summary>
        /// Loads the Assembly bytecode into the current AppDomain.
        /// <para>
        /// The contract should always be the only exported type.
        /// </para>
        /// </summary>
        private static Type Load(byte[] byteCode, string typeName)
        {
            Assembly contractAssembly = Assembly.Load(byteCode);
            return contractAssembly.ExportedTypes.FirstOrDefault(x=>x.Name == typeName);
        }

        internal void LogExecutionContext(ILogger logger, IBlock block, IMessage message, uint160 contractAddress, IBaseContractTransactionData callData)
        {
            var builder = new StringBuilder();

            builder.Append(string.Format("{0}:{1},{2}:{3},", nameof(block.Coinbase), block.Coinbase, nameof(block.Number), block.Number));
            builder.Append(string.Format("{0}:{1},", nameof(contractAddress), contractAddress.ToAddress(this.network)));
            builder.Append(string.Format("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(message.ContractAddress), message.ContractAddress, nameof(message.GasLimit), message.GasLimit, nameof(message.Sender), message.Sender, nameof(message.Value), message.Value));

            if (callData.MethodParameters != null && callData.MethodParameters.Length > 0)
                builder.Append(string.Format(",{0}:{1}", nameof(callData.MethodParameters), callData.MethodParameters));

            logger.LogTrace("{0}", builder.ToString());
        }
    }
}