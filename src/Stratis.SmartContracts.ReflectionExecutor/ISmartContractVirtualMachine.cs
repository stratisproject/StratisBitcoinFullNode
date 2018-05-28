using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.ReflectionExecutor
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
