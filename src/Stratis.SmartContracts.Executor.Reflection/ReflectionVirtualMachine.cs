using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;
using Stratis.SmartContracts.Executor.Reflection.Lifecycle;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly ILogger logger;
        public static int VmVersion = 1;

        public ReflectionVirtualMachine(
            InternalTransactionExecutorFactory internalTransactionExecutorFactory,
            ILoggerFactory loggerFactory)
        {
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public VmExecutionResult Create(byte[] contractCode,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IPersistentState persistentState,
            IContractStateRepository repository, 
            CallData callData)
        {
            this.logger.LogTrace("()");

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToConstructor(callData.ContractExecutionCode);

            Type contractType = Load(gasInjectedCode);

            var internalTransferList = new List<TransferInfo>();

            IInternalTransactionExecutor internalTransactionExecutor = this.internalTransactionExecutorFactory.Create(repository, internalTransferList);

            var balanceState = new BalanceState(repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(callData.ContractAddress));

            // Invoke the constructor of the provided contract code
            LifecycleResult result = SmartContractConstructor.Construct(contractType, contractState, callData.MethodParameters);

            if (!result.Success)
            {
                LogException(result.Exception);

                this.logger.LogTrace("(-)[CREATE_CONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

                return VmExecutionResult.Error(gasMeter.GasConsumed, result.Exception.InnerException ?? result.Exception);
            }
            else
                this.logger.LogTrace("[CREATE_CONTRACT_INSTANTIATION_SUCCEEDED]");

            this.logger.LogTrace("(-):{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

            return VmExecutionResult.Success(internalTransferList, gasMeter.GasConsumed, result.Object);
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public VmExecutionResult ExecuteMethod(byte[] contractCode,
            string contractMethodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IPersistentState persistentState, 
            IContractStateRepository repository)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(contractMethodName), contractMethodName);

            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();

            if (contractMethodName == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_METHODNAME_NOT_GIVEN]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, null);

            }

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToContractMethod(contractCode, contractMethodName);
            Type contractType = Load(gasInjectedCode);
            if (contractType == null)
            {
                this.logger.LogTrace("(-)[CALLCONTRACT_CONTRACTTYPE_NULL]");
                return VmExecutionResult.Error(gasMeter.GasConsumed, null);
            }

            var internalTransferList = new List<TransferInfo>();

            IInternalTransactionExecutor internalTransactionExecutor = this.internalTransactionExecutorFactory.Create(repository, internalTransferList);

            var balanceState = new BalanceState(repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(context.ContractAddress));

            LifecycleResult result = SmartContractRestorer.Restore(contractType, contractState);

            if (!result.Success)
            {
                LogException(result.Exception);

                this.logger.LogTrace("(-)[CALLCONTRACT_INSTANTIATION_FAILED]:{0}={1}", nameof(gasMeter.GasConsumed), gasMeter.GasConsumed);

                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                executionResult.GasConsumed = gasMeter.GasConsumed;

                return VmExecutionResult.Error(gasMeter.GasConsumed, result.Exception.InnerException ?? result.Exception);
            }
            else
                this.logger.LogTrace("[CALL_CONTRACT_INSTANTIATION_SUCCEEDED]");

            object methodResult = null;

            try
            {
                MethodInfo methodToInvoke = contractType.GetMethod(contractMethodName);
                if (methodToInvoke == null)
                    throw new ArgumentException(string.Format("[CALLCONTRACT_METHODTOINVOKE_NULL_DOESNOT_EXIST]:{0}={1}", nameof(contractMethodName), contractMethodName));

                if (methodToInvoke.IsConstructor)
                    throw new ConstructorInvocationException("[CALLCONTRACT_CANNOT_INVOKE_CTOR]");

                SmartContract smartContract = result.Object;
                methodResult = methodToInvoke.Invoke(smartContract, context.Parameters);
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
        private static Type Load(byte[] byteCode)
        {
            Assembly contractAssembly = Assembly.Load(byteCode);
            return contractAssembly.ExportedTypes.FirstOrDefault();
        }
    }
}