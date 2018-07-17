using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(IGasMeter gasMeter,
            IContractStateRepository repository,
            CallData callData, ITransactionContext transactionContext);

        VmExecutionResult ExecuteMethod(byte[] contractCode,
            string methodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter, IPersistentState persistentState, IContractStateRepository repository);
    }
}
