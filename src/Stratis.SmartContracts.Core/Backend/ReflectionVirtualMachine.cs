using System;
using System.Reflection;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.Lifecycle;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        public static int VmVersion = 1;
        private readonly IPersistentState persistentState;

        public ReflectionVirtualMachine(IPersistentState persistentState)
        {
            this.persistentState = persistentState;
        }

        /// <summary>
        /// Creates a new instance of a smart contract by invoking the contract's constructor
        /// </summary>
        public ISmartContractExecutionResult Create(
            byte[] contractCode, 
            string contractTypeName,
            ISmartContractExecutionContext context, 
            IGasMeter gasMeter, 
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance)
        {
            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();

            var contractAssembly = Assembly.Load(contractCode);
            Type contractType = contractAssembly.GetType(contractTypeName);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTxExecutor,
                new InternalHashHelper(),
                getBalance);

            // Invoke the constructor of the provided contract code
            LifecycleResult result = SmartContractConstructor
                .Construct(contractType, contractState, context.Parameters);

            executionResult.GasConsumed = gasMeter.GasConsumed;

            if (!result.Success)
            {
                executionResult.Exception = result.Exception.InnerException ?? result.Exception;
                return executionResult;
            }

            executionResult.Return = result.Object;

            return executionResult;
        }

        /// <summary>
        /// Invokes a method on an existing smart contract
        /// </summary>
        public ISmartContractExecutionResult ExecuteMethod(
            byte[] contractCode,
            string contractTypeName,
            string contractMethodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance)
        {
            ISmartContractExecutionResult executionResult = new SmartContractExecutionResult();
            if (contractMethodName == null)
                return executionResult;

            var contractAssembly = Assembly.Load(contractCode);
            Type contractType = contractAssembly.GetType(contractTypeName);

            var contractState = new SmartContractState(
                context.Block,
                context.Message,
                this.persistentState,
                gasMeter,
                internalTxExecutor,
                new InternalHashHelper(),
                getBalance);

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
    }
}