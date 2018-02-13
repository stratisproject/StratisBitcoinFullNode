using System;
using System.Reflection;
using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts.Backend
{
    internal class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private const string InitMethod = "Init";

        public IContractStateRepository StateDb { get; }

        public ReflectionVirtualMachine(IContractStateRepository stateDb)
        {
            this.StateDb = stateDb;
        }

        public SmartContractExecutionResult ExecuteMethod(byte[] contractCode, SmartContractExecutionContext context)
        {
            SetStaticValues(context);

            var assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(context.ContractTypeName);

            var contract = (CompiledSmartContract)Activator.CreateInstance(type);

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