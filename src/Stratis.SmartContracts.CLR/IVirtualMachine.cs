using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface IVirtualMachine
    {
        VmExecutionResult Create(IStateRepository repository, 
            ISmartContractState contractState,
            IResourceMeter gasMeter,
            byte[] contractCode,
            object[] parameters,
            string typeName = null);

        VmExecutionResult ExecuteMethod(ISmartContractState contractState,
            IResourceMeter gasMeter,
            MethodCall methodCall,
            byte[] contractCode, string typeName);
    }
}
