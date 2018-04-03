using System;

namespace Stratis.SmartContracts.Core.Backend
{
    public interface ISmartContractVirtualMachine
    {
        ISmartContractExecutionResult Create(
            byte[] contractCode,
            string contractTypeName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance);

        ISmartContractExecutionResult ExecuteMethod(
            byte[] contractCode, 
            string contractTypeName,
            string methodName,
            ISmartContractExecutionContext context, 
            IGasMeter gasMeter,
            IInternalTransactionExecutor internalTxExecutor,
            Func<ulong> getBalance);
    }
}
