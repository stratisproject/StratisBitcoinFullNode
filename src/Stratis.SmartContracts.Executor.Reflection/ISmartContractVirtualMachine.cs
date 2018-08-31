using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractVirtualMachine
    {
        VmExecutionResult Create(
            IGasMeter gasMeter,
            IContractState repository,
            ICreateData createData, 
            ITransactionContext transactionContext,
            string typeName = null);

        VmExecutionResult ExecuteMethod(
            IGasMeter gasMeter,
            IContractState repository,
            ICallData callData, 
            ITransactionContext transactionContext);
    }
}
