using System;

namespace Stratis.SmartContracts.Core.Backend
{
    public interface ISmartContractVirtualMachine
    {
        ISmartContractExecutionResult Create(byte[] contractCode,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance);

        ISmartContractExecutionResult ExecuteMethod(byte[] contractCode,
            string methodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance);
    }
}
