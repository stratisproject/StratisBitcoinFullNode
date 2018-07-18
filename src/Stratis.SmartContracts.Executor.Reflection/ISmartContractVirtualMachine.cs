using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(IGasMeter gasMeter,
            IContractStateRepository repository,
            CallData callData, ITransactionContext transactionContext);

        VmExecutionResult ExecuteMethod(IGasMeter gasMeter,
            IContractStateRepository repository,
            CallData callData, ITransactionContext transactionContext);
    }
}
