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
        public IContractStateRepository StateDb { get; private set; }

        public ReflectionVirtualMachine(IContractStateRepository stateDb)
        {
            this.StateDb = stateDb;
        }

        public SmartContractExecutionResult ExecuteMethod(byte[] contractCode, SmartContractExecutionContext context)
        {
            SetStaticValues(context);
            Assembly assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(context.ContractTypeName);
            SmartContract contract = (SmartContract)Activator.CreateInstance(type);
            object result = null;
            if (context.ContractMethod != null)
            {
                MethodInfo methodToInvoke = type.GetMethod(context.ContractMethod);
                result = methodToInvoke.Invoke(contract, context.Parameters);
            }
            return new SmartContractExecutionResult
            {
                GasUsed = contract.GasUsed,
                Return = result
            };
        }

        private void SetStaticValues(SmartContractExecutionContext context)
        {
            Block.Set(context.BlockNumber, context.CoinbaseAddress, context.Difficulty);
            Message.Set(new Address(context.ContractAddress), new Address(context.CallerAddress), context.CallValue, context.GasLimit);
            PersistentState.ResetCounter();
            PersistentState.SetDbAndAddress(this.StateDb, context.ContractAddress);
        }
    }
}
