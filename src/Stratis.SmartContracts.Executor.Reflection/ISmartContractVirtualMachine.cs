using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(MethodCall methodCall,
            ISmartContractState contractState,
            byte[] contractCode,
            string typeName = null);

        VmExecutionResult ExecuteMethod(MethodCall methodCall,
            ISmartContractState contractState, byte[] contractCode, string typeName);
    }
}
