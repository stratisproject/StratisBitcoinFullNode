using System;

namespace Stratis.SmartContracts.Core.Backend
{
    public interface ISmartContractVirtualMachine
    {
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
