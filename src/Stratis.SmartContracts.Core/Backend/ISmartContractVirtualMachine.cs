using System;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.Backend
{
    public interface ISmartContractVirtualMachine
    {
        ISmartContractExecutionResult Create(byte[] contractCode,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter);

        ISmartContractExecutionResult ExecuteMethod(byte[] contractCode,
            string methodName,
            ISmartContractExecutionContext context,
            IGasMeter gasMeter);
    }
}
