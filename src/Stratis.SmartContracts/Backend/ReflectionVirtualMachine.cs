using System;
using System.Reflection;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    internal class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        public static int VmVersion = 1;
        private readonly PersistentState persistentState;
        private const string InitMethod = "Init";

        public ReflectionVirtualMachine(PersistentState persistentState)
        {
            this.persistentState = persistentState;
        }

        public SmartContractExecutionResult ExecuteMethod(
            byte[] contractCode, 
            string contractTypeName, 
            string contractMethodName, 
            SmartContractExecutionContext context,
            GasMeter gasMeter)
        {
            var assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(contractTypeName);

            var state = new SmartContractState(
                context.Block, 
                context.Message, 
                this.persistentState, 
                gasMeter);
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