using Stratis.SmartContracts.State;
using System;
using System.Reflection;
using System.Linq;
using Stratis.SmartContracts.ContractValidation;

namespace Stratis.SmartContracts.Backend
{
    internal class ReflectionVirtualMachine : ISmartContractVirtualMachine
    {
        private const string InitMethod = "Init";

        public ISmartContractStateRepository StateDb { get; private set; }

        public ReflectionVirtualMachine(ISmartContractStateRepository stateDb)
        {
            StateDb = stateDb;
        }

        public SmartContractExecutionResult ExecuteMethod(byte[] contractCode, SmartContractExecutionContext context)
        {
            SetStaticValues(context);
            Assembly assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(context.ContractTypeName);
            CompiledSmartContract contract = (CompiledSmartContract)Activator.CreateInstance(type);
            MethodInfo methodToInvoke = type.GetMethod(context.ContractMethod);
            methodToInvoke.Invoke(context, context.Parameters);
            return new SmartContractExecutionResult
            {
                GasUsed = contract.GasUsed
            };
        }

        private void SetStaticValues(SmartContractExecutionContext context)
        {
            Block.Set(context.BlockNumber, context.BlockHash, context.CoinbaseAddress, context.Difficulty);
            Message.Set(new Address(context.ContractAddress), new Address(context.CallerAddress), context.CallValue, context.GasLimit);
            PersistentState.ResetCounter();
            PersistentState.SetDbAndAddress(this.StateDb, context.ContractAddress);
        }
    }
}
