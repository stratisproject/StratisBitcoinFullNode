using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface IVirtualMachine
    {
        VmExecutionResult Create(IStateRepository repository, 
            ISmartContractState contractState,
            RuntimeObserver.IGasMeter gasMeter,
            byte[] contractCode,
            object[] parameters,
            string typeName = null);

        VmExecutionResult ExecuteMethod(ISmartContractState contractState, 
            RuntimeObserver.IGasMeter gasMeter,
            MethodCall methodCall,
            byte[] contractCode, string typeName);
    }
}
