using System;
using System.Reflection;

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
            var assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(contractTypeName);

            var state = new SmartContractState(
                context.Block, 
                context.Message, 
                this.persistentState,
                gasMeter,
                internalTxExecutor,
                getBalance);

            var contract = (SmartContract)Activator.CreateInstance(type, state);

            var executionResult = new SmartContractExecutionResult();
            if (contractMethodName == null)
                return executionResult;

            MethodInfo methodToInvoke = type.GetMethod(contractMethodName);

            try
            {
                executionResult.Return = methodToInvoke.Invoke(contract, context.Parameters);
            }
            catch (TargetInvocationException targetException)
            {
                executionResult.Exception = targetException.InnerException ?? targetException;
            }
            finally
            {
                executionResult.GasUnitsUsed = gasMeter.ConsumedGas;
            }

            return executionResult;
        }
    }
}