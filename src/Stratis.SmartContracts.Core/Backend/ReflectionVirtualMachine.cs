using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.Lifecycle;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        public static int VmVersion = 1;
        private readonly IPersistentState persistentState;
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly IContractStateRepository repository;

        public ReflectionVirtualMachine(IPersistentState persistentState,
            InternalTransactionExecutorFactory internalTransactionExecutorFactory, IContractStateRepository repository)
        {
            this.persistentState = persistentState;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.repository = repository;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public ISmartContractExecutionResult Create(byte[] contractCode,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter)
        {
            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToConstructor(contractCode);

            Type contractType = Load(gasInjectedCode);

            var internalTransferList = new InternalTransferList();

            IInternalTransactionExecutor internalTransactionExecutor =
                this.internalTransactionExecutorFactory.Create(this.repository, internalTransferList);
            
            var balanceState = new BalanceState(this.repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(context.ContractAddress));

            // Invoke the constructor of the provided contract code
            LifecycleResult result = SmartContractConstructor
                .Construct(contractType, contractState, context.Parameters);

            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();

            executionResult.GasConsumed = gasMeter.GasConsumed;

            if (!result.Success)
            {
                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                return executionResult;
            }

            executionResult.Return = result.Object;

            executionResult.InternalTransfers = internalTransferList.Transfers;

            return executionResult;
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public ISmartContractExecutionResult ExecuteMethod(byte[] contractCode,
            string contractMethodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter)
        {
            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();
            
            if (contractMethodName == null)
                return executionResult;

            byte[] gasInjectedCode = SmartContractGasInjector.AddGasCalculationToContractMethod(contractCode, contractMethodName);
            
            Type contractType = Load(gasInjectedCode);

            if (contractType == null)
                return executionResult;

            var internalTransferList = new InternalTransferList();

            IInternalTransactionExecutor internalTransactionExecutor =
                this.internalTransactionExecutorFactory.Create(this.repository, internalTransferList);

            var balanceState = new BalanceState(this.repository, context.Message.Value, internalTransferList);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTransactionExecutor,
                new InternalHashHelper(),
                () => balanceState.GetBalance(context.ContractAddress));

            LifecycleResult result = SmartContractRestorer.Restore(contractType, contractState);

            if (!result.Success)
            {
                // If contract instantiation failed, return any gas consumed.
                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                executionResult.GasConsumed = gasMeter.GasConsumed;
                return executionResult;
            }

            SmartContract smartContract = result.Object;

            try
            {
                MethodInfo methodToInvoke = contractType.GetMethod(contractMethodName);

                if (methodToInvoke.IsConstructor)
                {
                    throw new ConstructorInvocationException("Cannot invoke constructor");
                }

                executionResult.Return = methodToInvoke.Invoke(smartContract, context.Parameters);

                executionResult.InternalTransfers = internalTransferList.Transfers;
            }
            catch (ArgumentException argumentException)
            {
                executionResult.Exception = argumentException;
            }
            catch (TargetInvocationException targetException)
            {
                executionResult.Exception = targetException.InnerException ?? targetException;
            }
            catch (TargetParameterCountException parameterExcepion)
            {
                executionResult.Exception = parameterExcepion;
            }
            catch (ConstructorInvocationException constructorInvocationException)
            {
                executionResult.Exception = constructorInvocationException;
            }
            finally
            {
                executionResult.GasConsumed = gasMeter.GasConsumed;
            }

            return executionResult;
        }

        /// <summary>
        /// Loads the Assembly bytecode into the current AppDomain
        /// </summary>
        /// <param name="byteCode"></param>
        /// <returns></returns>
        private static Type Load(byte[] byteCode)
        {
            Assembly contractAssembly = Assembly.Load(byteCode);

            // The contract should always be the only exported type
            return contractAssembly.ExportedTypes.FirstOrDefault();
        }
    }
}