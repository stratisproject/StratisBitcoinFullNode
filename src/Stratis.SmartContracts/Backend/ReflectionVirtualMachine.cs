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

        internal IStateDb StateDb { get; set; }

        public ReflectionVirtualMachine(IStateDb stateDb)
        {
            StateDb = stateDb;
        }

        public ExecutionResult CreateContract(byte[] contractCode, ExecutionContext executionContext)
        {
            var analyzer = new ContractAnalyzer(contractCode, executionContext.ContractTypeName);
            var analyzeResult = analyzer.ValidateContract();
            if (!analyzeResult.Valid)
                throw new NotImplementedException("Need to handle what happens with gas etc if contract isn't valid");

            byte[] adjustedCode = analyzer.InjectSpendGas();

            // Create the context to be injected into the block
            Block.Set(executionContext.BlockNumber, executionContext.BlockHash, executionContext.CoinbaseAddress, executionContext.Difficulty);
            Message.Set(new Address(executionContext.ContractAddress), new Address(executionContext.CallerAddress), executionContext.CallValue, executionContext.GasLimit);
            PersistentState.ResetCounter();
            PersistentState.SetDbAndAddress(StateDb, executionContext.ContractAddress);

            // Initialise the assembly and contract object
            Assembly assembly = Assembly.Load(adjustedCode);
            Type type = assembly.GetType(executionContext.ContractTypeName);

            // Invoke constructor
            CompiledSmartContract contractObject = (CompiledSmartContract) Activator.CreateInstance(type);

            var initMethod = type.GetMethods().FirstOrDefault(x => x.GetCustomAttribute(typeof(SmartContractInitAttribute)) != null);
            if (initMethod != null)
                initMethod.Invoke(contractObject, executionContext.Parameters);

            return new ExecutionResult
            {
                GasUsed = contractObject.GasUsed,
                NewContractAddress = executionContext.ContractAddress,
                Return = adjustedCode
            };
        }


        public ExecutionResult LoadContractAndRun(byte[] contractCode, ExecutionContext executionContext)
        {
            // Create the context to be injected into the block
            Block.Set(executionContext.BlockNumber, executionContext.BlockHash, executionContext.CoinbaseAddress, executionContext.Difficulty);
            Message.Set(new Address(executionContext.ContractAddress), new Address(executionContext.CallerAddress), executionContext.CallValue, executionContext.GasLimit);
            PersistentState.SetDbAndAddress(StateDb, executionContext.ContractAddress);
            PersistentState.ResetCounter();

            // Initialise the assembly and contract object
            Assembly assembly = Assembly.Load(contractCode);
            Type type = assembly.GetType(executionContext.ContractTypeName);

            var contractObject = (CompiledSmartContract) Activator.CreateInstance(type);

            var methodToInvoke = type.GetMethod(executionContext.ContractMethod);
            var result = methodToInvoke.Invoke(contractObject, executionContext.Parameters);

            return new ExecutionResult
            {
                GasUsed = contractObject.GasUsed,
                Return = result
            };
        }
    }
}
