using System;
using System.Reflection;
using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts.Backend
{
    /// <summary>
    /// Used to instantiate smart contracts using reflection and then execute certain methods and their parameters.
    /// </summary>
    internal class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private readonly PersistentState _persistentState;
        private const string InitMethod = "Init";

        public ReflectionVirtualMachine(PersistentState persistentState)
        {
            this._persistentState = persistentState;
        }

        public SmartContractExecutionResult ExecuteMethod(byte[] contractCode, string contractTypeName, string contractMethodName,
            SmartContractExecutionContext context)
        {            
            Assembly assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(contractTypeName);

            // @TODO - SmartContractState is basically the same thing as SmartContractExecutionContext so merge them eventually
            var state = new SmartContractState(context.Block, context.Message, this._persistentState);

            SmartContract contract = (SmartContract)Activator.CreateInstance(type, state);

            object result = null;
            if (contractMethodName != null)
            {
                MethodInfo methodToInvoke = type.GetMethod(contractMethodName);
                result = methodToInvoke.Invoke(contract, context.Parameters);
            }
            return new SmartContractExecutionResult
            {
                GasUsed = contract.GasUsed,
                Return = result
            };
        }        
    }
}
