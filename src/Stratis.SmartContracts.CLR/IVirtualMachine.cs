using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public interface IVirtualMachine
    {
        VmExecutionResult Create(IStateRepository repository, ISmartContractState contractState,
            byte[] contractCode,
            object[] parameters,
            string typeName = null);

        VmExecutionResult ExecuteMethod(ISmartContractState contractState, MethodCall methodCall,
            byte[] contractCode, string typeName);
    }
}
