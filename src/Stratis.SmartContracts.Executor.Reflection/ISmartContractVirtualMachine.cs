using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(
            IGasMeter gasMeter,
            IContractStateRepository repository,
            ICreateData createData, 
            ITransactionContext transactionContext);

        VmExecutionResult ExecuteMethod(
            IGasMeter gasMeter,
            IContractStateRepository repository,
            ICallData callData, 
            ITransactionContext transactionContext);
    }
}
