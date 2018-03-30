using System;
using System.Reflection;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    public class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        public static int VmVersion = 1;
        private readonly IPersistentState persistentState;
        private const string InitMethod = "Init";

        public ReflectionVirtualMachine(IPersistentState persistentState)
        {
            this.persistentState = persistentState;
        }

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

            SmartContract smartContract = null;

            try
            {
                smartContract = (SmartContract)Activator.CreateInstance(contractType, contractState);
            }
            catch (Exception exception)
            {
                // If contract instantiation failed, return any gas consumed.
                executionResult.Exception = exception.InnerException ?? exception;
                executionResult.GasConsumed = gasMeter.GasConsumed;
                return executionResult;
            }

            try
            {
                MethodInfo methodToInvoke = contractType.GetMethod(contractMethodName);
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
            finally
            {
                executionResult.GasConsumed = gasMeter.GasConsumed;
            }

            return executionResult;
        }
    }
}