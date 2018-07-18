using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(
            byte[] contractCode,
            string typeName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IPersistentState persistentState,
            IContractStateRepository repository);

        VmExecutionResult ExecuteMethod(byte[] contractCode,
            string methodName,
            string typeName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IPersistentState persistentState,
            IContractStateRepository repository);
    }
}
